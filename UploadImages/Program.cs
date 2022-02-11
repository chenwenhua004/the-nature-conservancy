using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TestProject1;

namespace UploadImages
{
    public class Program
    {
        readonly static String connectionString = "";

        static void Main()
        {
            Console.WriteLine($"----------start to upload the photos at {DateTime.UtcNow}----------------");
            GetDirectories(@"D:\TNC\Pictures");
            Console.WriteLine($"----------upload the photos successfully at {DateTime.UtcNow}---------------");

            var requestId = GetRequestId();
            var detections = GetDetections(requestId);

            //var detections = "https://cameratrap.blob.core.windows.net/batch-api/api_cm/job_36a31de073244eb69daf42dddc27844f/36a31de073244eb69daf42dddc27844f_detections__2021-10-05T05%3A00%3A47.257639Z.json?se=2022-04-03T05%3A20%3A50Z&sp=rt&sv=2020-04-08&sr=b&sig=YE49Mv3lgUfeTVaCQr0iA7nVTST6CtxdDWU6EIkCeYM%3D";
            GetResult(detections);

            Console.ReadLine();
        }

        public static void GetDirectories(string rootDirectory)
        {
            var directories = Directory.EnumerateDirectories(rootDirectory, "*.*", SearchOption.AllDirectories);

            var enu = directories.GetEnumerator();

            List<Task> tasks = new List<Task>();
            while (enu.MoveNext())
            {
                string directory = enu.Current;
                IEnumerable<string> subDirectories = Directory.EnumerateDirectories(directory, "*.*", SearchOption.TopDirectoryOnly);
                if (!subDirectories.GetEnumerator().MoveNext())
                {

                    Task task = UploadImages(rootDirectory, directory);
                    tasks.Add(task);

                    //limited the max thread count is 10
                    if (tasks.Count > 10)
                    {
                        Task.WaitAny(tasks.ToArray());
                        tasks = tasks.Where(t => t.Status != TaskStatus.RanToCompletion).ToList();
                    }
                }
            }

            Task.WaitAll(tasks.ToArray());
            //bool allCompleted = Task.WaitAll(tasks.ToArray(), 100000);
        }

        public static async Task UploadImages(string rootDirectory, string directory)
        {
            var containerName = directory.Substring(rootDirectory.Length + 1).Replace('\\', '-');
            Console.WriteLine($"---------{containerName}-----------");

            //Create a unique name for the container
            BlobContainerClient containerClient = new BlobContainerClient(connectionString, containerName);
            await containerClient.CreateIfNotExistsAsync();

            //get the namelists of images already uploaded
            var hs = new HashSet<string>();
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                hs.Add(blobItem.Name);
            }

            string[] localFilePaths = Directory.GetFiles(directory);

            int errorCount = 0;
            for (var i = 0; i < localFilePaths.Length; i++)
            {
                var fileName = Path.GetFileName(localFilePaths[i]);

                if (hs.Contains(fileName))
                {
                    Console.WriteLine($"Image {fileName} already exist!");
                    continue;
                }

                BlobClient blobClient = containerClient.GetBlobClient(fileName);

                var count = 3;
                while (count-- > 0)
                {
                    try
                    {
                        await blobClient.UploadAsync(localFilePaths[i], true);
                        Console.WriteLine($"Image {fileName} uploaded successfully!");
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Image:{fileName} uploaded failed!");
                        Console.WriteLine(e.Message);
                    }

                    if (count == 0)
                    {
                        errorCount++;
                    }
                }
            }

            Console.WriteLine($"Totally {errorCount} images uploaded failed!");
        }

        public static async Task DownLoadImages(BlobContainerClient containerClient)
        {
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                var downloadPath = "C:/Users/dongl/Desktop/DownloadFromBlob/" + blobItem.Name;
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);
                //Console.WriteLine(blobItem.Properties.ContentLength);
                await blobClient.DownloadToAsync(downloadPath);
            }
        }

        public static string GetRequestId()
        {
            //var url = "https://reqbin.com/echo/post/json";
            var url = "http://otter.southcentralus.cloudapp.azure.com:6007/v4/camera-trap/detection-batch/request_detections";

            var httpRequest = (HttpWebRequest)WebRequest.Create(url);
            httpRequest.Method = "POST";
            httpRequest.Accept = "application/json";
            httpRequest.ContentType = "application/json";
            httpRequest.Host = "otter.southcentralus.cloudapp.azure.com:6007";

            var data = @"{  
				""input_container_sas"": ""https://mytnc.blob.core.windows.net/images?sp=rl&st=2021-09-30T20:51:52Z&se=2022-10-01T04:51:52Z&spr=https&sv=2020-08-04&sr=c&sig=S4an%2F8Cx26V3bpTAwBTD0S0Jhi1dSuyOcjAbs3o1VhA%3D"",
				""model_version"": ""4.1"",
				""caller"": ""dongl@microsoft.com"",
				""country"": ""China"",
				""organization_name"": ""The Nature Conservancy"",
				""first_n"": 10
				}";

            using (var streamWriter = new StreamWriter(httpRequest.GetRequestStream()))
            {
                streamWriter.Write(data);
            }

            var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                Console.WriteLine(result);
                JObject json = JObject.Parse(result);
                var id = json.Value<string>("request_id");
                Console.WriteLine(id);
                return id;
            }
        }

        public static string GetDetections(string requestId)
        {
            var url = "http://otter.southcentralus.cloudapp.azure.com:6007/v4/camera-trap/detection-batch/task/36a31de073244eb69daf42dddc27844f";
            var httpRequest = (HttpWebRequest)WebRequest.Create(url);
            httpRequest.Method = "GET";
            httpRequest.Accept = "application/json";
            httpRequest.Host = "otter.southcentralus.cloudapp.azure.com:6007";

            var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            JToken detections = null;
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                Console.WriteLine(result);
                JObject json = JObject.Parse(result);
                detections = (((json["Status"] as JObject)["message"] as JObject)["output_file_urls"] as JObject)["detections"];
                Console.WriteLine(detections.Value<string>());
            }

            return detections.Value<string>();
        }

        public static string GetResult(string detectionsUrl)
        {
            var httpRequest = (HttpWebRequest)WebRequest.Create(detectionsUrl);
            httpRequest.Method = "GET";
            httpRequest.Accept = "application/json";

            var httpResponse = (HttpWebResponse)httpRequest.GetResponse();
            var result = "";
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            var logger = new Logger();
            logger.Trace("start---------------------------");
            logger.Trace("GetResult successfully. The result is " + result + ".");

            return result;
        }
    }
}
