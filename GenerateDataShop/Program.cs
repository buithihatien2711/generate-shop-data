using CsvHelper;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace GenerateDataShop
{
    public class Program
    {
        private static IMongoCollection<CategoryEntity> _categoryCollection;
        private static IMongoCollection<ProductEntity> _productCollection;
        private static readonly HttpClient client = new HttpClient();
        private static readonly string csvFileName = "product_data.csv";
        private static readonly string connectionString = "mongodb://14.225.207.46:27017";
        private static readonly string apiUrl = "http://14.225.207.46:7000/processimage";

        static async Task Main(string[] args)
        {
            var client = new MongoClient(connectionString);
            var restaurantDb = client.GetDatabase("ecommerce-test");
            _categoryCollection = restaurantDb.GetCollection<CategoryEntity>("categories");
            _productCollection = restaurantDb.GetCollection<ProductEntity>("products");

            string workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.Parent.FullName;
            string csvFilePath = Path.Combine(projectDirectory, csvFileName);

            var productDtos = ReadCsvFile(csvFilePath);

            var products = new List<ProductEntity>();
            var rand = new Random();

            foreach (var productDto in productDtos)
            {
                ObjectId categoryId;
                var productImageUrls = JsonConvert.DeserializeObject<List<string>>(productDto.images);
                var productImages = new List<string>();
                var productId = ObjectId.GenerateNewId();
                foreach (var productImageUrl in productImageUrls)
                {
                    var childFolderName = productId.ToString();
                    var imageFolderName = "ProductImages";
                    var childFolderPath = Path.Combine(projectDirectory, imageFolderName, childFolderName);

                    if (!Directory.Exists(childFolderPath))
                    {
                        Directory.CreateDirectory(childFolderPath);
                    }

                    var productImage = $"image_{Guid.NewGuid()}.jpg";
                    var productImagePath = Path.Combine(childFolderPath, productImage);
                    try
                    {
                        await DownloadImageAsync(productImageUrl, productImagePath);
                        await ProcessImage(productImagePath, apiUrl);
                    }
                    catch (ExternalException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    catch (ArgumentNullException ex)
                    {
                        Console.WriteLine(ex.Message);
                        // Something wrong with Stream
                    }
                }
                var findCategory = await _categoryCollection.AsQueryable().FirstOrDefaultAsync(e => e.cName == productDto.category);
                if (findCategory == null)
                {
                    var newCategory = new CategoryEntity()
                    {
                        cName = productDto.category,
                        cStatus = "Active"
                    };
                    await _categoryCollection.InsertOneAsync(newCategory);
                    categoryId = newCategory._id;
                }
                else
                {
                    categoryId = findCategory._id;
                }

                var newProduct = new ProductEntity()
                {
                    _id = productId,
                    pName = productDto.name,
                    pDescription = productDto.description,
                    pPrice = rand.Next(99, 700) * 1000,
                    pSold = 0,
                    pQuantity = rand.Next(100, 1000),
                    pCategory = categoryId,
                    pImages = null,
                    pOffer = 0.ToString(),
                    pRatingReviews = new List<RatingReviewEntity>(),
                    pStatus = "Active"
                };
                products.Add(newProduct);
                //var filePath = baseFileUrl + newProduct.pImages.FirstOrDefault();
            }

            await _productCollection.InsertManyAsync(products);
            Console.WriteLine("Add successfully");
        }

        private static List<ProductDto> ReadCsvFile(string filePath)
        {
            var products = new List<ProductDto>();
            using (var reader = new StreamReader(filePath))
            {
                using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
                {
                    products = csv.GetRecords<ProductDto>().ToList();
                    Console.WriteLine(products);
                }
            }
            return products;
        }

        private static async Task ProcessImage(string filePath, string apiUrl)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"{filePath} does not exist");
                return;
            }

            string imageName = Path.GetFileName(filePath);

            using (var content = new MultipartFormDataContent())
            {
                var fileContent = new StreamContent(File.OpenRead(filePath));
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                content.Add(fileContent, "image", Path.GetFileName(filePath));
                content.Add(new StringContent(imageName), "imageId");

                var response = await client.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"{filePath} uploaded successfully!");
                }
                else
                {
                    Console.WriteLine($"{filePath} uploaded fail!");
                }
            }
        }

        static async Task DownloadImageAsync(string url, string savePath)
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var data = await response.Content.ReadAsByteArrayAsync();
                await System.IO.File.WriteAllBytesAsync(savePath, data);
            }
        }
    }
}
