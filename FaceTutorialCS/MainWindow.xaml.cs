// <snippet_using>
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// </snippet_using>

namespace FaceTutorial
{
    public partial class MainWindow : Window
    {
        private static string subscriptionKey = "b9c29eabf9f54b128af66ba6ee3aa2c3";
        private static string endpoint = "https://nelrusocr.cognitiveservices.azure.com/";

        // the Batch Read method endpoint
        static string uriBase = endpoint + "/vision/v3.0/read/analyze";

        public MainWindow()
        {
            InitializeComponent();

        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Get the image file to scan from the user.
            var openDlg = new Microsoft.Win32.OpenFileDialog();

            openDlg.Filter = "JPEG Image(*.jpg)|*.jpg";
            bool? result = openDlg.ShowDialog(this);

            // Return if canceled.
            if (!(bool)result)
            {
                return;
            }

            // Display the image file.
            string filePath = openDlg.FileName;

            Uri fileUri = new Uri(filePath);
            BitmapImage bitmapSource = new BitmapImage();

            bitmapSource.BeginInit();
            bitmapSource.CacheOption = BitmapCacheOption.None;
            bitmapSource.UriSource = fileUri;
            bitmapSource.EndInit();

            FacePhoto.Source = bitmapSource;

            Title = "NELRUS OCR";

            await ReadText(filePath);
        }


        /// <summary>
        /// Gets the text from the specified image file by using
        /// the Computer Vision REST API.
        /// </summary>
        /// <param name="imageFilePath">The image file with text.</param>
        static async Task ReadText(string imageFilePath)
        {
            try
            {
                HttpClient client = new HttpClient();

                // Request headers.
                client.DefaultRequestHeaders.Add(
                    "Ocp-Apim-Subscription-Key", subscriptionKey);

                string url = uriBase;

                HttpResponseMessage response;

                // Two REST API methods are required to extract text.
                // One method to submit the image for processing, the other method
                // to retrieve the text found in the image.

                // operationLocation stores the URI of the second REST API method,
                // returned by the first REST API method.
                string operationLocation;

                // Reads the contents of the specified local image
                // into a byte array.
                byte[] byteData = GetImageAsByteArray(imageFilePath);

                // Adds the byte array as an octet stream to the request body.
                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    // This example uses the "application/octet-stream" content type.
                    // The other content types you can use are "application/json"
                    // and "multipart/form-data".
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    // The first REST API method, Batch Read, starts
                    // the async process to analyze the written text in the image.
                    response = await client.PostAsync(url, content);
                }

                // The response header for the Batch Read method contains the URI
                // of the second method, Read Operation Result, which
                // returns the results of the process in the response body.
                // The Batch Read operation does not return anything in the response body.
                if (response.IsSuccessStatusCode)
                    operationLocation =
                        response.Headers.GetValues("Operation-Location").FirstOrDefault();
                else
                {
                    // Display the JSON error data.
                    string errorString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("\n\nResponse:\n{0}\n",
                        JToken.Parse(errorString).ToString());
                    return;
                }

                // If the first REST API method completes successfully, the second 
                // REST API method retrieves the text written in the image.
                //
                // Note: The response may not be immediately available. Text
                // recognition is an asynchronous operation that can take a variable
                // amount of time depending on the length of the text.
                // You may need to wait or retry this operation.
                //
                // This example checks once per second for ten seconds.
                string contentString;
                int i = 0;
                do
                {
                    System.Threading.Thread.Sleep(1000);
                    response = await client.GetAsync(operationLocation);
                    contentString = await response.Content.ReadAsStringAsync();
                    ++i;
                }
                while (i < 60 && contentString.IndexOf("\"status\":\"succeeded\"") == -1);

                if (i == 60 && contentString.IndexOf("\"status\":\"succeeded\"") == -1)
                {
                    Console.WriteLine("\nTimeout error.\n");
                    return;
                }


                var result = JsonConvert.DeserializeObject<Root>(contentString);


                string resultString = "Result: \n";

                foreach (var item in result.analyzeResult.readResults)
                {
                    if (item != null)
                    {
                        //foreach (var lines in item.lines)
                        //{

                        //    resultString += "\n" + lines.text;
                        //}

                        for (int y = 0; y < item.lines.Count(); y++)
                        {
                            if (item.lines[y].text.ToString().Contains("NOME"))
                            {
                                resultString += "\n Nome: " + item.lines[y + 1].text;
                            }
                            if (item.lines[y].text.ToString().Contains("DOC.IDENTIDADE / ORG.EMISSOR UF"))
                            {
                                resultString += "\n Identidade + Orgão Emissor: " + item.lines[y + 1].text + " - " + item.lines[y + 2].text;
                            }
                            if (item.lines[y].text.ToString().Contains("CPF"))
                            {
                                resultString += "\n CPF: " + item.lines[y + 2].text;
                            }
                            if (item.lines[y].text.ToString().Contains("FILIAÇÃO"))
                            {
                                resultString += "\n Filiação: " + item.lines[y + 1].text + " e " + item.lines[y + 2].text;
                            }
                            if (item.lines[y].text.ToString().Contains("Nº REGISTRO"))
                            {
                                resultString += "\n Nº Registro: " + item.lines[y + 3].text;
                            }
                        }
                    }
                }


                MessageBox.Show(resultString, "Result");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
            }
        }

        /// <summary>
        /// Returns the contents of the specified file as a byte array.
        /// </summary>
        /// <param name="imageFilePath">The image file to read.</param>
        /// <returns>The byte array of the image data.</returns>
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            // Open a read-only file stream for the specified file.
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                // Read the file's contents into a byte array.
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }


    }

    public class Word
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public double confidence { get; set; }
    }

    public class Line
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public List<Word> words { get; set; }
    }

    public class ReadResult
    {
        public int page { get; set; }
        public int angle { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string unit { get; set; }
        public List<Line> lines { get; set; }
    }

    public class AnalyzeResult
    {
        public string version { get; set; }
        public List<ReadResult> readResults { get; set; }
    }

    public class Root
    {
        public string status { get; set; }
        public DateTime createdDateTime { get; set; }
        public DateTime lastUpdatedDateTime { get; set; }
        public AnalyzeResult analyzeResult { get; set; }
    }

}
