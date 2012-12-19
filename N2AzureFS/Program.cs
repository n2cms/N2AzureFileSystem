using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace N2AzureFS
{
    class Program
    {
        public class Test
        {
            private readonly CloudBlobClient blobStorage;
            private readonly CloudBlobContainer blobContainer;

            public Test(CloudStorageAccount account)
            {
                blobStorage = account.CreateCloudBlobClient();
                blobContainer = blobStorage.GetContainerReference("testing");
                blobContainer.CreateIfNotExists();

                this.blobContainer.SetPermissions(new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Container
                });
            }

            //public Test(string accountName, string accountKey, string rootName, string storageUrlFormat = "http://{0}.blob.core.windows.net")
            //{
            //    var key = Convert.FromBase64String(accountKey);
            //    var creds = new StorageCredentials(accountName, key);
            //    var baseUri = string.Format(storageUrlFormat, accountName);

            //    this.rootName = String.Format("{0}/{1}", baseUri, blobContainer.Name);
            //    this.blobStorage = new CloudBlobClient(new Uri(baseUri), creds);

            //    this.blobContainer = blobStorage.GetContainerReference(rootName);
            //    this.blobContainer.CreateIfNotExists();

            //    var perms = new BlobContainerPermissions
            //    {
            //        PublicAccess = BlobContainerPublicAccessType.Container
            //    };
            //    this.blobContainer.SetPermissions(perms);
            //}

            public void Create(string id, Stream data)
            {
                
                ICloudBlob blob = blobContainer.GetBlockBlobReference(id);

                blob.UploadFromStream(data);
                blob.Properties.ContentType = "text/plain";
                blob.SetProperties();

                blob.Metadata["WhenFileUploadedUtc"] = DateTime.UtcNow.ToLongTimeString();
                blob.SetMetadata();
            }


            public static void Leased(CloudBlockBlob blob, Action<CloudBlockBlob> a)
            {
                var lease = blob.AcquireLease(TimeSpan.FromSeconds(60), null);
                try
                {
                    a(blob);
                }
                finally
                {
                    blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(lease));
                }
            }

            public void Copy(string id, string newId)
            {
                var blob = blobContainer.GetBlobReferenceFromServer(id) as CloudBlockBlob;

                var newBlob = blobContainer.GetBlockBlobReference(newId);

                Leased(blob, b =>
                    {
                        var async = newBlob.BeginStartCopyFromBlob(blob, null, null);
                        async.AsyncWaitHandle.WaitOne();
                    });

                
            }

            public IEnumerable<string> GetDirectory(string id)
            {
                var blob = blobContainer.GetDirectoryReference(id);
                return blob.ListBlobs().OfType<CloudBlockBlob>().Select(x => x.Name);
            }

            public IEnumerable<string> GetDirectory2(string id)
            {
                var blob = blobContainer.GetDirectoryReference(id);

                return blobContainer.ListBlobs(id, true).OfType<CloudBlockBlob>().Select(x => x.Name);
            }

            public IEnumerable<string> GetSubDirectories(string id)
            {
                var blob = blobContainer.GetDirectoryReference(id);
                return blob.ListBlobs().OfType<CloudBlobDirectory>().Select(x => x.Prefix);
            }

            public void CreateDirectory(string id)
            {
            }

            public bool DirExists(string id)
            {
                return true;
            }

            public static Stream FromString(string data)
            {
                return new MemoryStream(Encoding.UTF8.GetBytes(data));
            }
        }



        static void Main()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);


            var fs = new AzureFileSystem(storageAccount.CreateCloudBlobClient().GetContainerReference("le-upload"));

            //var fileStream = new FileStream(@"C:\Users\PHNI\Downloads\Git-1.8.0-preview20121022.exe", FileMode.Open);
            var fileStream = new FileStream(@"C:\Users\PHNI\Downloads\desktop.ini", FileMode.Open);
            
            fs.CreateDirectory("~/cux/bux");
            fs.WriteFile("~/cux/bux/foo_bar_342x234.txt", fileStream);
            //fs.CopyFile("~/foo/bar/cux.ini", "~/cux/bloop.txt");

            

            //fs.MoveDirectory("~/cux/bux", "~/");


            ////var fileStream = Test.FromString("trololol");

            //var test = new Test(storageAccount);
            ////test.Create("~/foo/bar", fileStream);
            ////test.Create("~/foo/baz", fileStream);
            //test.Copy("le git", "le got");

            ////test.GetSubDirectories("~/foo").ToList().ForEach(Console.WriteLine);
            //test.GetDirectory2("~").ToList().ForEach(Console.WriteLine);



        }
    }
}
