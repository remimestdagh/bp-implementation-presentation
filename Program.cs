
using Azure;
using Azure.AI.FormRecognizer;
using Azure.AI.FormRecognizer.Models;
using Azure.AI.FormRecognizer.Training;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace bp_implementation
{
    class Program
    {
        private readonly string testDataContainerName = "testdata-facturen";
        private readonly string trainedDataContainerName = "final-bp";
        private readonly BlobContainerClient containerClient;
        private readonly BlobContainerClient containerTestClient;
        private readonly string blobConnection = "BLOB CONN STRING";
        private readonly string endpoint = "https://formrecremi.cognitiveservices.azure.com/";
        private readonly string apiKey = "API KEY FORM RECOGNIZER";
        private readonly AzureKeyCredential credential;
        private readonly FormRecognizerClient client;
        private FormTrainingClient formTrainingClient;
        private readonly string blobContainerUri = "BLOB SAS";
        public Program()
        {
            credential = new AzureKeyCredential(this.apiKey);
            client = new FormRecognizerClient(new Uri(this.endpoint), this.credential);
            formTrainingClient = new FormTrainingClient(new Uri(this.endpoint), this.credential);
            containerClient = new BlobContainerClient(this.blobConnection, this.trainedDataContainerName);
            containerTestClient = new BlobContainerClient(this.blobConnection, this.testDataContainerName);
        }

        static async Task Main(string[] args)
        {


            Program p = new Program();
            await p.MainLoop();

        }

        private async Task ConnectToBlob()
        {
            

            Console.WriteLine("Listing blobs...");
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
            {
                Console.WriteLine("\t" + blobItem.Name);
            }
        }

        private async Task MainLoop()
        {
            Console.WriteLine("Welcome");
            bool go = true;
            while (go)
            {
                Console.WriteLine("\nChoose option \n");
                Console.WriteLine("0: exit");
                Console.WriteLine("1: Info");
                Console.WriteLine("2: Recognize using trained model");
                Console.WriteLine("3: Browse models");
                Console.WriteLine("4: Delete model");
                Console.WriteLine("5: Show testdata");
          
                string inputString = Console.ReadLine();
                try
                {
                    int input = int.Parse(inputString);
                    switch (input)
                    {
                        case 1:
                            this.ShowInfo();
                            break;
                    
                        case 2:
                            Console.WriteLine("Type name of file and press ENTER");
                            string fileName = Console.ReadLine();
                            await this.AnalyzePdfForm(this.client, fileName);
                            go = false;
                            break;
                        case 3:
                            await this.ShowModels();

                            break;
                        case 4:
                            await this.DeleteModel();
                            break;
                        case 5:
                            await this.ConnectToBlob();
                            break;
                        case 0:
                            go = false;
                            break;
                        default:
                            break;
                    }
                }
                catch
                {
                    Console.WriteLine("Please give a number in the correct range");
                    continue;
                }
            }

        }
        private void ShowInfo()
        {
            Console.WriteLine("Made by Remi Mestdagh");
            Console.WriteLine("2: Choose a local file that is in the data folder of this project to upload and extract data in JSON format");
            Console.WriteLine("3: Shows all models and their name");
            Console.WriteLine("4: Delete a model");
            Console.WriteLine("5: Displays all files in storage container.");

        }

        private async Task DeleteModel()
        {
            Dictionary<int, CustomFormModelInfo> modelDict = await ShowModels();
            foreach (var k in modelDict)
            {
                Console.WriteLine("Key: " + k.Key + "   |   id:" + k.Value.ModelId);
            }
            bool go = true;
            while (go)
            {
                Console.WriteLine("Enter the key of the model you wish you destroy");
                string inputString = Console.ReadLine();
                try
                {
                    int input = int.Parse(inputString);

                    Response r = await this.formTrainingClient.DeleteModelAsync(modelDict.GetValueOrDefault(input).ModelId);
                    Console.WriteLine(r.ToString());
                    break;

                }
                catch
                {
                    Console.WriteLine("Please give a number in the correct range");
                    continue;
                }
            }
        }
       
        private async Task<Dictionary<int, CustomFormModelInfo>> ShowModels()
        {
            AsyncPageable<CustomFormModelInfo> modelsInfo = this.formTrainingClient.GetCustomModelsAsync();

            Dictionary<int, CustomFormModelInfo> modelDict = new();
            int i = 0;
            await foreach (CustomFormModelInfo secretProperties in modelsInfo)
            {
                i++;
                Console.WriteLine(i + ": " + secretProperties.ModelId + " " + secretProperties.ModelName);
                modelDict.Add(i, secretProperties);
            }
            return modelDict;
        }

        private async Task<Uri> UploadTestImg(string fileName)
        {

            string localPath = "../../../data/";
            string localFilePath = Path.Combine(localPath, fileName);
            BlobClient blobClient = this.containerTestClient.GetBlobClient(fileName);
            
            Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);
            using FileStream uploadFileStream = File.OpenRead(localFilePath);
            await blobClient.UploadAsync(uploadFileStream, new BlobHttpHeaders{ ContentType = "image/png"});
            uploadFileStream.Close();
            return blobClient.Uri;
        }

        private async Task AnalyzePdfForm(FormRecognizerClient recognizerClient, string fileName) {
            Uri fileUri = await this.UploadTestImg(fileName);
            CustomFormModelInfo info = null;
            Dictionary<int, CustomFormModelInfo> modelDict = await ShowModels();
             foreach (var k in modelDict)
             {
                 Console.WriteLine("Key: " + k.Key + "   |   id: " + k.Value.ModelId + "   |   name: " + k.Value.ModelName);
             }

             Console.WriteLine("Enter the key of the model you wish to use");
             string inputString = Console.ReadLine();
             try
             {
                 int input = int.Parse(inputString);
                 info = modelDict.GetValueOrDefault(input);
             }
             catch
             {
                 Console.WriteLine("Please give a number in the correct range");
             }

            if (info != null)
            {
                RecognizedFormCollection forms = await recognizerClient
           .StartRecognizeCustomFormsFromUri(info.ModelId, fileUri)
           .WaitForCompletionAsync();
                foreach (RecognizedForm form in forms)
                {
                    Console.WriteLine($"Form of type: {form.FormType}");
                    foreach (FormField field in form.Fields.Values)
                    {
                        Console.WriteLine($"Field '{field.Name}: ");

                        if (field.LabelData != null)
                        {
                            Console.WriteLine($"    Label: '{field.LabelData.Text}");
                        }
                        if(field.ValueData != null)
                        {
                            if(field.Name == "naam aankoper" && field.ValueData.Text.Contains("Dhr. "))
                            {
                                Console.WriteLine($"    Value: '{field.ValueData.Text.Substring(5)}");
                            } else
                            {
                                Console.WriteLine($"    Value: '{field.ValueData.Text}");
                            }
                           
                        }

                        Console.WriteLine($"    Confidence: '{field.Confidence}");
                    }
                    Console.WriteLine("Table data:");
                    foreach (FormPage page in form.Pages)
                    {
                        for (int i = 0; i < page.Tables.Count; i++)
                        {
                            FormTable table = page.Tables[i];
                            Console.WriteLine($"Table {i} has {table.RowCount} rows and {table.ColumnCount} columns.");
                            foreach (FormTableCell cell in table.Cells)
                            {
                                Console.WriteLine($"    Cell ({cell.RowIndex}, {cell.ColumnIndex}) contains {(cell.IsHeader ? "header" : "text")}: '{cell.Text}'");
                            }
                        }
                    }
                }
            }

        }
    }
}
