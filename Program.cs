using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using lobe;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using lobe.ImageSharp;
using Microsoft.Extensions.Configuration;

namespace ClassifyDynamoGraph
{
    class Program
    {
        private static readonly IConfiguration Configuration =
            new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

        static void Main(string[] args)
        {
            var signatureFilePath = Configuration["sig_file_path"];
            var imageToClassify = Configuration["image_to_classify"];


            using (WebClient client = new WebClient())
            {
                client.DownloadFile(new Uri(imageToClassify), @"c:\temp\image.png");
            }

            ImageClassifier.Register("onnx", () => new OnnxImageClassifier());
            using var classifier = ImageClassifier.CreateFromSignatureFile(
                new FileInfo(signatureFilePath));

            var results = classifier.Classify(Image
                .Load(@"c:\temp\image.png").CloneAs<Rgb24>());
            Console.WriteLine(results.Classifications.First().Label);
        }
    }
}
