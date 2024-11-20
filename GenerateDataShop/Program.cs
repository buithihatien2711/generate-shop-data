namespace GenerateDataShop
{
    public class Program
    {
        private static IMongoCollection<CategoryEntity> _categoryCollection;
        private static IMongoCollection<ProductEntity> _productCollection;
        private static readonly HttpClient client = new HttpClient();
        private static readonly string csvFileName = "result.csv";
        private static readonly string connectionString = "mongodb://127.0.0.1:27017";
        private static readonly string apiUrl = "http://172.31.20.210:7000/processimage";
        private static readonly string baseFileUrl = "C:/temp/selected_images/";

        static async Task Main(string[] args)
        {
            var client = new MongoClient(connectionString);
            var restaurantDb = client.GetDatabase("ecommerce");
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
                string categoryId;
                var productImageUrls = JsonConvert.DeserializeObject<List<string>>(productDto.images);
                var productImages = new List<string>();
                foreach (var productImageUrl in productImageUrls)
                {
                    var productImage = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var productImagePath = Path.Combine(projectDirectory, productImage);
                    try
                    {
                        await DownloadImage(productImageUrl, productImagePath);
                    }
                    catch (ExternalException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                    catch (ArgumentNullException)
                    {
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
                    categoryId = newCategory.Id;
                }
                else
                {
                    categoryId = findCategory.Id;
                }

                var newProduct = new ProductEntity()
                {
                    pName = productDto.name,
                    pDescription = productDto.description,
                    pPrice = rand.Next(99, 500) * 1000,
                    pSold = 0,
                    pQuantity = rand.Next(100, 1000) * 1000,
                    pCategory = categoryId,
                    pImages = null,
                    pOffer = 0.ToString(),
                    pRatingReviews = new List<RatingReviewEntity>(),
                    pStatus = "Active"
                };
                products.Add(newProduct);
                //var filePath = baseFileUrl + newProduct.pImages.FirstOrDefault();
                //await ProcessImage(filePath, apiUrl);
            }

            await _productCollection.InsertManyAsync(products);
            Console.WriteLine("Add successfully");

            //    int productOrder = 0;
            //    string csvPath = "C:\\test\\DownloadedImages\\result.csv";
            //    string outputFolder = "C:\\test\\DownloadedImages"; // Folder to save images.

            //    if (!Directory.Exists(outputFolder))
            //        Directory.CreateDirectory(outputFolder);

            //    //List<List<string>> imageUrls = ReadCsvFile(csvPath);

            //    List<List<string>> imageUrls = new List<List<string>>()
            //{
            //    new List<string>()
            //    {
            //        "https://images.asos-media.com/products/new-look-trench-coat-in-camel/204351106-4?$n_1920w$&wid=1926&fit=constrain",
            //        "https://images.asos-media.com/products/new-look-trench-coat-in-camel/204351106-1-neutral?$n_1920w$&wid=1926&fit=constrain"
            //    },
            //    new List<string>()
            //    {
            //        "https://images.asos-media.com/products/new-look-trench-coat-in-camel/204351106-4?$n_1920w$&wid=1926&fit=constrain",
            //        "https://images.asos-media.com/products/new-look-trench-coat-in-camel/204351106-1-neutral?$n_1920w$&wid=1926&fit=constrain"
            //    }
            //};

            //    //using (var driver = new ChromeDriver())
            //    //{
            //    foreach (var urlList in imageUrls)
            //    {
            //        var childFolderName = $"Product_{productOrder}";
            //        var childFolderPath = Path.Combine(outputFolder, childFolderName);

            //        if (!Directory.Exists(childFolderPath))
            //        {
            //            Directory.CreateDirectory(childFolderPath);
            //        }

            //        foreach (var url in urlList)
            //        {
            //            try
            //            {
            //                //string imageName = Path.GetFileName(new Uri(url).LocalPath); // Extract image name.
            //                string imageName = $"image_{Guid.NewGuid()}.jpeg";
            //                string savePath = Path.Combine(childFolderPath, imageName);

            //                if (System.IO.File.Exists(savePath))
            //                {
            //                    Console.WriteLine($"Image already exists: {savePath}");
            //                    continue;
            //                }

            //                //driver.Navigate().GoToUrl(url); // Navigate to the URL.

            //                // Download image using HTTP client.
            //                await DownloadImageAsync(url, savePath);
            //                Console.WriteLine($"Downloaded: {savePath}");
            //            }
            //            catch (Exception ex)
            //            {
            //                Console.WriteLine($"Error downloading {url}: {ex.Message}");
            //            }
            //        }
            //        productOrder++;
            //        //}
            //    }
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

        static List<List<string>> ReadCsvFile2(string filePath)
        {
            var result = new List<List<string>>();
            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var row = csv.GetField<string>(9); // Adjust index if necessary.
                    if (row == null || row == String.Empty)
                    {
                        continue;
                    }
                    var urls = JsonConvert.DeserializeObject<List<string>>(row);
                    result.Add(urls);
                }
            }
            return result;
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
