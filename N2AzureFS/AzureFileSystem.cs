using System.Configuration;
using System.Diagnostics.Contracts;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using N2.Configuration;
using N2.Edit;
using N2.Edit.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using N2.Engine;
using N2.Persistence.NH;


namespace N2AzureFS
{
    internal static class Helper
    {
        public static FileData GetFileData(this ICloudBlob blob)
        {
            if (blob == null)
                return new FileData();

            var lastTime = blob.Properties.LastModified == null
                               ? DateTime.UtcNow
                               : blob.Properties.LastModified.Value.DateTime;

            return new FileData
            {
                Name = Path.GetFileName(blob.Name),
                Created = lastTime,
                Length = blob.Properties.Length,
                Updated = lastTime,
                VirtualPath = blob.Name
            };
        }

        public static DirectoryData GetDirectoryData(this CloudBlobDirectory blob)
        {
            return new DirectoryData
            {
                Name = blob.Prefix.TrimEnd('/').Split('/').Last(),
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
                VirtualPath = blob.Prefix.TrimEnd('/') + '/',
            };
        }

        public static void Leased(this CloudBlockBlob blob, Action<CloudBlockBlob> a)
        {
            var lease = blob.AcquireLease(null, null);
            try
            {
                a(blob);
            }
            finally
            {
                blob.ReleaseLease(AccessCondition.GenerateLeaseCondition(lease));
            }
        }

        [Pure]
        public static string Canonicalize(this string virtualPath)
        {
            return virtualPath.TrimStart('~');
        }

        [Pure]
        public static string CanonicalizeDirectory(this string virtualDirectory)
        {
            return virtualDirectory.TrimStart('~').TrimEnd('/') + "/";
        }
    }

    [Service]
    public class AzureFileSystem : IFileSystem
    {
        private readonly CloudBlobContainer container;

        public AzureFileSystem(ISessionProvider sessionProvider, DatabaseSection config)
        {
            var storageAccount = CloudStorageAccount.Parse(
                ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);

            this.container = storageAccount.CreateCloudBlobClient().GetContainerReference("le-upload");

            container.CreateIfNotExists();
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Container });

            CreateDirectory("/upload/");
        }

        public AzureFileSystem(CloudBlobContainer container)
        {
            // this is not the constructor you're looking for

            this.container = container;
            container.SetPermissions(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Container });

            container.CreateIfNotExists();

            CreateDirectory("");
        }

        public IEnumerable<FileData> GetFiles(string parentVirtualPath)
        {
            parentVirtualPath = parentVirtualPath.CanonicalizeDirectory();
            return container.ListBlobs(parentVirtualPath).OfType<CloudBlockBlob>().Where(b => ! b.Name.EndsWith("/")).Select(Helper.GetFileData).ToList();
        }

        public FileData GetFile(string virtualPath)
        {
            virtualPath = virtualPath.Canonicalize();
            return container.GetBlockBlobReference(virtualPath).GetFileData();
        }

        public IEnumerable<DirectoryData> GetDirectories(string parentVirtualPath)
        {
            parentVirtualPath = parentVirtualPath.CanonicalizeDirectory();
            return container.ListBlobs(parentVirtualPath).OfType<CloudBlobDirectory>().Select(Helper.GetDirectoryData);
        }

        public DirectoryData GetDirectory(string virtualPath)
        {
            virtualPath = virtualPath.CanonicalizeDirectory();
            return container.GetDirectoryReference(virtualPath).GetDirectoryData();
        }

        public bool FileExists(string virtualPath)
        {
            virtualPath = virtualPath.Canonicalize();
            return !virtualPath.EndsWith("/") && container.GetBlockBlobReference(virtualPath).Exists();
        }

        private void MoveFileNoEvent(string fromVirtualPath, string destinationVirtualPath)
        {
            fromVirtualPath = fromVirtualPath.Canonicalize();
            destinationVirtualPath = destinationVirtualPath.Canonicalize();

            if (fromVirtualPath == destinationVirtualPath)
                return;
            CopyFileNoEvent(fromVirtualPath, destinationVirtualPath);
            DeleteFileNoEvent(fromVirtualPath);   
        }

        public void MoveFile(string fromVirtualPath, string destinationVirtualPath)
        {
            MoveFileNoEvent(fromVirtualPath, destinationVirtualPath);

            if (this.FileMoved != null) this.FileMoved(this, new FileEventArgs(destinationVirtualPath, fromVirtualPath));
        }

        private void CopyFileNoEvent(string source, string dest)
        {
            source = source.Canonicalize();
            dest = dest.Canonicalize();

            var blobFrom = container.GetBlockBlobReference(source);
            var blobTo = container.GetBlockBlobReference(dest);

            blobFrom.Leased(b =>
                blobTo.BeginStartCopyFromBlob(b, null, null).AsyncWaitHandle.WaitOne());

        }

        private void DeleteFileNoEvent(string virtualPath)
        {
            virtualPath = virtualPath.Canonicalize();
            container.GetBlobReferenceFromServer(virtualPath).Delete();
        }

        public void DeleteFile(string virtualPath)
        {
            DeleteFileNoEvent(virtualPath);
            if (this.FileDeleted != null) this.FileDeleted(this, new FileEventArgs(virtualPath, null));
        }

        public void CopyFile(string source, string dest)
        {
            CopyFileNoEvent(source, dest);  
            if (this.FileCopied != null) this.FileCopied(this, new FileEventArgs(dest, source));
        }

        public Stream OpenFile(string virtualPath, bool readOnly = false)
        {
            virtualPath = virtualPath.Canonicalize();

            var blob = container.GetBlockBlobReference(virtualPath);
            var stream = new MemoryStream();

            if (readOnly)
                blob.DownloadToStream(stream);
            else
                blob.UploadFromStream(stream);

            return stream;
        }

        public void WriteFile(string virtualPath, Stream inputStream)
        {
            virtualPath = virtualPath.Canonicalize();

            var blob = container.GetBlockBlobReference(virtualPath);
            blob.Properties.ContentType = virtualPath.GetMimeType();
            blob.UploadFromStream(inputStream);

            //if (this.FileWritten != null) this.FileWritten((object)this, new FileEventArgs(virtualPath, null));
        }

        public void ReadFileContents(string virtualPath, Stream outputStream)
        {
            virtualPath = virtualPath.Canonicalize();

            var blob = container.GetBlockBlobReference(virtualPath);
            blob.Leased(b => b.DownloadToStream(outputStream));
        }

        public bool DirectoryExists(string virtualPath)
        {
            virtualPath = virtualPath.CanonicalizeDirectory();
            return container.GetDirectoryReference(virtualPath).ListBlobs().Any();
        }

        public void MoveDirectory(string fromVirtualPath, string destinationVirtualPath)
        {
            fromVirtualPath = fromVirtualPath.CanonicalizeDirectory();
            destinationVirtualPath = destinationVirtualPath.CanonicalizeDirectory();

            if (destinationVirtualPath.Trim('~', '/').StartsWith(fromVirtualPath.Trim('~', '/')))
                throw new ArgumentException(fromVirtualPath + " is a subdirectory of " + destinationVirtualPath, "fromVirtualPath");

            foreach (var blob in container.ListBlobs(fromVirtualPath, true).OfType<CloudBlockBlob>())
            {
                var toPath = destinationVirtualPath.TrimEnd('/') +
                             "/" + blob.Name.Substring(fromVirtualPath.Length).TrimStart('/');

                MoveFileNoEvent(blob.Name, toPath);
            }

            if (this.DirectoryMoved != null) this.DirectoryMoved(this, new FileEventArgs(fromVirtualPath, destinationVirtualPath));
        }

        public void DeleteDirectory(string virtualPath)
        {
            virtualPath = virtualPath.CanonicalizeDirectory();

            foreach (var blob in container.ListBlobs(virtualPath, true).OfType<CloudBlockBlob>())
                blob.Delete();

            if (this.DirectoryDeleted != null) this.DirectoryDeleted(this,new FileEventArgs(virtualPath, null));
        }

        public void CreateDirectory(string virtualPath)
        {
            virtualPath = virtualPath.CanonicalizeDirectory();

            virtualPath = virtualPath.TrimEnd('/') + "/";
            if (container.GetBlockBlobReference(virtualPath).Exists())
                return;
            container.GetBlockBlobReference(virtualPath)
                .UploadFromStream(new MemoryStream(Encoding.UTF8.GetBytes("I'm a directory")));

            if (this.DirectoryCreated != null) this.DirectoryCreated(this, new FileEventArgs(virtualPath, null));
        }

        public event EventHandler<FileEventArgs> FileWritten;
        public event EventHandler<FileEventArgs> FileCopied;
        public event EventHandler<FileEventArgs> FileMoved;
        public event EventHandler<FileEventArgs> FileDeleted;
        public event EventHandler<FileEventArgs> DirectoryCreated;
        public event EventHandler<FileEventArgs> DirectoryMoved;
        public event EventHandler<FileEventArgs> DirectoryDeleted;


    }
}
