using Enyim.Caching;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Transactions;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace md.akharinkhabar.ir.Models
{
    public class MediaStream : Controller
    {
        //
        // GET: /MediaStream/

        public ActionResult Index()
        {
            return View();
        }

        #region Var
        public string ID { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public string FileExt { get; set; }
        public string ServerPath { get; set; }
        public byte[] ServerTxn { get; set; }
        private readonly string conStr;
        // Chunk file size in byte
        public const int ReadStreamBufferSize = 256 * 1024;
        private readonly bool inBuffer = false;
        readonly string bufferPath;
        #endregion

        #region Constructor
        public MediaStream(string _id)
        {
            ID = _id;
            conStr = WebConfigurationManager.ConnectionStrings["Media"].ConnectionString;

            //-------------Get Metadata of the file that previously save in the database--------------------------------------------------------
            SqlDataAdapter da = new SqlDataAdapter("select FileSize,FileType,FileExt from MediaStream where newsid = " + ID, conStr);
            DataTable dt = new DataTable();
            da.Fill(dt);
            if (dt.Rows.Count > 0)
            {
                FileSize = (long)dt.Rows[0]["FileSize"];
                FileType = dt.Rows[0]["FileType"].ToString();
                FileExt = dt.Rows[0]["FileExt"].ToString();
            }

            //-------------(It's optional) If found the media file in somewhere in the buffer it would set the flag = true--------------------------------------------------------
            bufferPath = "B:\\" + ID + FileExt;
            if (System.IO.File.Exists(bufferPath))
            {
                FileStream fis = null;
                try
                {
                    fis = System.IO.File.Open(bufferPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    inBuffer = true;
                }
                catch (Exception)
                {
                    inBuffer = false;
                }
                try
                {
                    if (fis != null)
                        fis.Close();
                }
                catch (Exception)
                {
                }

            }
            else
            {
                bufferPath = "F:\\" + ID + FileExt;
                if (System.IO.File.Exists(bufferPath))
                {
                    FileStream fis = null;
                    try
                    {
                        fis = System.IO.File.Open(bufferPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        inBuffer = true;
                    }
                    catch (Exception)
                    {
                        inBuffer = false;
                    }
                    try
                    {
                        if (fis != null)
                            fis.Close();
                    }
                    catch (Exception)
                    {
                    }

                }
            }
        } 
        #endregion

        /// <summary>
        /// Load Media From SQL Server FileStream in the form of chunked parts
        /// </summary>
        /// <param name="outputStream">If exists in local buffer (ram disk || fast tempropy disk) it won't go to fetch data from SQL Server</param>
        /// <param name="start">Start range byte of media to play</param>
        /// <param name="end">End range byte of media to play</param>
        /// <param name="id">Id of media to find</param>
        /// <returns></returns>
        public async Task CreatePartialContent(Stream outputStream, long start, long end, string id)
        {
            int count = 0;
            long remainingBytes = end - start + 1;
            long position = start;
            byte[] buffer = new byte[ReadStreamBufferSize];

            if (inBuffer) //---------------------------(It's optional) LOAD FROM BUFFER----------------------------------------------
            {
                try
                {
                    using (FileStream sfs = new FileStream(bufferPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, ReadStreamBufferSize, true))
                    {
                        sfs.Position = start;
                        do
                        {
                            try
                            {
                                count = await sfs.ReadAsync(buffer, 0, Math.Min((int)remainingBytes, ReadStreamBufferSize));
                                if (count <= 0) break;
                                await outputStream.WriteAsync(buffer, 0, count);
                                
                            }
                            catch (Exception)
                            {
                                return;
                            }
                            position = sfs.Position;
                            remainingBytes = end - position + 1;
                        } while (position <= end);
                    }
                }
                catch (Exception)
                {
                }
                finally
                {
                    await outputStream.FlushAsync();
                    outputStream.Close();
                }

            }
            else //---------------------------LOAD FROM SQL SERVER----------------------------------------------
            {
                try
                {
                    using (TransactionScope ts = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                    {
                        try
                        {
                            using (SqlDataAdapter da = new SqlDataAdapter("select FileData.PathName(),GET_FILESTREAM_TRANSACTION_CONTEXT() from MediaStream where newsid = " + ID, conStr))
                            {
                                DataTable dt = new DataTable();
                                da.Fill(dt);
                                ServerPath = dt.Rows[0][0].ToString();
                                ServerTxn = (byte[])dt.Rows[0][1];
                                dt.Dispose();
                            }
                        }
                        catch (Exception)
                        {
                            ts.Complete();
                            return;
                        }

                        using (SqlFileStream sfs = new SqlFileStream(ServerPath, ServerTxn, FileAccess.Read, FileOptions.Asynchronous, 0))
                        {
                            sfs.Position = start;
                            do
                            {
                                try
                                {
                                    count = await sfs.ReadAsync(buffer, 0, Math.Min((int)remainingBytes, ReadStreamBufferSize));
                                    if (count <= 0) break;
                                    await outputStream.WriteAsync(buffer, 0, count);
                                }
                                catch (Exception)
                                {
                                    break;
                                }
                                position = sfs.Position;
                                remainingBytes = end - position + 1;
                            } while (position <= end);

                        }


                        ts.Complete();
                    }
                }
                catch (Exception)
                {

                    return;
                }
                finally
                {
                    await outputStream.FlushAsync();
                    outputStream.Close();
                }

            }
        }
    }
}
