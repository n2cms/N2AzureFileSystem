using System;
using System.Dynamic;
using System.IO;

namespace N2AzureFS
{
    
    //class BlobReadWriteStream : Stream
    //{
    //    private readonly CloudBlockBlob blob;
    //    private Stream underlyingStream;
    //    private bool? isReadStream;

    //    public BlobReadWriteStream(CloudBlockBlob blob)
    //    {
    //        this.blob = blob;
    //    }

    //    public override bool CanRead
    //    {
    //        get { return isReadStream; }
    //    }

    //    public override bool CanSeek
    //    {
    //        get { return false; }
    //    }

    //    public override bool CanWrite
    //    {
    //        get { 
    //            if(!isReadStream.HasValue)
    //            return 
                
    //            !isReadStream; }
    //    }

    //    public override void Flush()
    //    {
    //        underlyingStream.Flush();
    //    }

    //    public override long Length
    //    {
    //        get { return blob.Properties.Length; }
    //    }

    //    public override long Position
    //    {
    //        get { return underlyingStream.Position; }
    //        set { underlyingStream.Position = value; }
    //    }

    //    public override int Read(byte[] buffer, int offset, int count)
    //    {
    //        PrepareRead();
           
    //        return underlyingStream.Read(buffer, offset, count);
    //    }

    //    private void PrepareRead()
    //    {
    //        Prepare(isRead: true);
    //    }

    //    public override long Seek(long offset, SeekOrigin origin)
    //    {
    //        throw new InvalidOperationException("cannot seek");
    //    }

    //    public override void SetLength(long value)
    //    {
    //        underlyingStream.SetLength(value);
    //    }

    //    public override void Write(byte[] buffer, int offset, int count)
    //    {
    //        PrepareWrite();
            
    //        underlyingStream.Write(buffer, offset, count);
    //    }

    //    private void PrepareWrite()
    //    {
    //        Prepare(isRead: false);
    //        throw new NotImplementedException();
    //    }

    //    private void Prepare(bool isRead)
    //    {
    //        if (!isReadStream.HasValue)
    //        {
    //            isReadStream = isRead;
    //            underlyingStream =
    //                isRead ? blob.OpenRead() : blob.OpenWrite();
    //        }
    //        else if (isRead ^ isReadStream.Value) throw new InvalidOperationException("invalid read or write request for blob state");
            
    //    }
    //}
}
