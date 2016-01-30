using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Model;
using RethinkDb.Driver.Tests;
using RethinkDb.Driver.Tests.Utils;
using RethinkDb.Driver.Utils;

namespace RethinkDb.Driver.ReGrid.Tests
{
    [TestFixture]
    public class BucketTests : QueryTestFixture
    {
        private Table fileTable;
        private Table chunkTable;
        private Db db;
        private string chunkIndex;
        private string fileIndexPath;
        private Bucket bucket;
        private string fileTableName;
        private string chunkTableName;


        public BucketTests()
        {
            Log.TruncateBinaryTypes = true;

            fileTable = r.db(DbName).table("fs_files");
            chunkTable = r.db(DbName).table("fs_chunk");
            db = r.db(DbName);
            var opts = new BucketConfig();
            chunkIndex = opts.ChunkIndex;
            fileIndexPath = opts.FileIndexPath;
            fileTableName = "fs_files";
            chunkTableName = "fs_chunks";
        }

        [SetUp]
        public void BeforeEachTest()
        {
            //make sure we get a new conn on each test.
            bucket = new Bucket(conn, DbName);
        }

        private void DropFilesTable()
        {
            var result = db.tableDrop(this.fileTableName).runResult(this.conn);
            result.AssertTablesDropped(1);
        }

        [Test]
        public void test_upload()
        {
            bucket.Drop();
            bucket.Initialize();

            var fileId = bucket.Upload("foobar.mp3", TestBytes.OneHalfChunk);

            fileId.Should().NotBeEmpty();

            var info = bucket.GetFileInfoByNameAsync("foobar.mp3", -1).WaitSync();

            info.Dump();

            info.Id.Should().Be(fileId);
        }

        [Test]
        public void test_download_as_bytes()
        {
            CreateBucketWithTwoFileRevisions();

            Console.WriteLine(">>>>> DOWNLOAD");
            var bytes = bucket.DownloadBytesByName("foobar.mp3");
            bytes.Should().Equal(TestBytes.OneHalfChunkReversed);

            bytes = bucket.DownloadBytesByName("foobar.mp3", revision: 0);
            bytes.Should().Equal(TestBytes.OneHalfChunk);
        }

        [Test]
        public void thow_exception_when_file_isnt_found()
        {
            bucket.Initialize();

            Action act = () => bucket.GetFileInfoByNameAsync("foooooooobar.mp3", -99)
                .WaitSync();

            act.ShouldThrow<FileNotFoundException>();
        }

        [Test]
        public void test_download_as_steream()
        {
            CreateBucketWithTwoFileRevisions();

            
            Console.WriteLine(">>>>> DOWNLOAD LATEST");
            var fs = File.Open("foobar_latest.mp3", FileMode.Create);
            bucket.DownloadToStreamByName("foobar.mp3", fs);
            fs.Close();

            var outBytes = File.ReadAllBytes("foobar_latest.mp3");

            TestBytes.OneHalfChunkReversed.Should().Equal(outBytes);



            Console.WriteLine(">>>>> DOWNLOAD ORIGINAL");

            fs = File.Open("foobar_original.mp3", FileMode.Create);
            bucket.DownloadToStreamByName("foobar.mp3", fs, revision: 0);
            fs.Close();

            outBytes = File.ReadAllBytes("foobar_original.mp3");

            TestBytes.OneHalfChunk.Should().Equal(outBytes);


        }


        [Test]
        [Explicit]
        public async Task path_ix_list_history()
        {
            //var files = this.fileTable
            //    .between(
            //        r.array(Status.Completed, "foobar.mp3", r.minval()),
            //        r.array(Status.Completed, "foobar.mp3", r.maxval())
            //        )[new {index = "path_ix"}]
            //    .orderBy()[new { index = r.desc("path_ix") }]
            //    .runCursor<FileInfo>(conn);

            var fileInfo = await bucket.GetFileInfoByNameAsync("foobar.mp3", -1);

            fileInfo.Dump();

        }

        [Test]
        public async Task path_ix_file_revision()
        {
            CreateBucketWithTwoFileRevisions();

            Console.WriteLine(">>>> LATEST ");

            var latest = await bucket.GetFileInfoByNameAsync("foobar.mp3", -1);

            latest.Dump();

            Console.WriteLine(">>>> ORIGINAL ");

            var original = await bucket.GetFileInfoByNameAsync("foobar.mp3", 0);

            original.Dump();

            original.UploadDate.Should().BeBefore(latest.UploadDate.Value);
        }

        private void CreateBucketWithTwoFileRevisions()
        {
            bucket.Drop();
            bucket.Initialize();
            
            //original reversed
            bucket.Upload("foobar.mp3", TestBytes.OneHalfChunk);

            Thread.Sleep(1500);

            //latest
            bucket.Upload("foobar.mp3", TestBytes.OneHalfChunkReversed);
        }
    }
}