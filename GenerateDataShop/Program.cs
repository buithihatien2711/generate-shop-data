namespace GenerateDataShop
{
    public class Program
    {
        private static IMongoCollection<CategoryEntity> _categoryCollection;
        private static IMongoCollection<ProductEntity> _productCollection;
        private static readonly HttpClient client = new HttpClient();
        private static readonly string csvFilePath = "";
        private static readonly string connectionString = "";
        // process image api url
        private static readonly string apiUrl = "";
        // Folder contain image
        private static readonly string baseFileUrl = "";

        static async Task Main(string[] args)
        {
            var client = new MongoClient(connectionString);
            var restaurantDb = client.GetDatabase("ecommerce");
            _categoryCollection = restaurantDb.GetCollection<CategoryEntity>("categories");
            _productCollection = restaurantDb.GetCollection<ProductEntity>("products");

            var productDtos = ReadCsvFile(csvFilePath);

            var products = new List<ProductEntity>();
            var rand = new Random();
            foreach (var productDto in productDtos)
            {
                string categoryId;

                var findCategory = await _categoryCollection.AsQueryable().FirstOrDefaultAsync(e => e.cName == productDto.product_type);
                if (findCategory == null)
                {
                    var newCategory = new CategoryEntity()
                    {
                        cName = productDto.product_type,
                        cStatus = "Active"
                    };
                    await _categoryCollection.InsertOneAsync(newCategory);
                    categoryId = newCategory.Id;
                }
                else
                {
                    categoryId = findCategory.Id;
                }

                var newProduct = new ProductEntity()
                {
                    pName = productDto.caption,
                    pDescription = productDto.caption,
                    pPrice = rand.Next(99, 500),
                    pSold = 0,
                    pQuantity = rand.Next(100, 1000),
                    pCategory = categoryId,
                    pImages = new List<string> { productDto.path },
                    pOffer = 0.ToString(),
                    pRatingReviews = new List<RatingReviewEntity>(),
                    pStatus = "Active"
                };
                products.Add(newProduct);
                var filePath = baseFileUrl + newProduct.pImages.FirstOrDefault();
                await ProcessImage(filePath, apiUrl);
            }

            await _productCollection.InsertManyAsync(products);
            Console.WriteLine("Add successfully");
            //Console.ReadKey();
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
                    Console.WriteLine($"{filePath} uploaded successfully!");
                }
            }
        }
    }
}
