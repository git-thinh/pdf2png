using PdfiumViewer;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace pdf2png
{
    class Program
    {
        static int m_port_write = 0;
        static int m_port_read = 0;

        #region [ CODE ]

        static RedisBase m_subcriber;
        static byte[] _pageAsBitmapBytes(PdfDocument doc, int pageCurrent)
        {
            int w = (int)doc.PageSizes[pageCurrent].Width;
            int h = (int)doc.PageSizes[pageCurrent].Height;

            ////if (w >= h) w = this.Width;
            ////else w = 1200;
            //if (w < 1200) w = 1200;
            //h = (int)((w * doc.PageSizes[i].Height) / doc.PageSizes[i].Width);

            if (w > 1200)
            {
                w = 1200;
                h = (int)((w * doc.PageSizes[pageCurrent].Height) / doc.PageSizes[pageCurrent].Width);
            }

            using (var image = doc.RenderTransparentBG(pageCurrent, w, h, 100, 100))
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
        static void __createDocumentBackground(string file)
        {
            if (File.Exists(file))
            {
                var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, m_port_write));
                if (!redis._connected) return;
                using (var doc = PdfDocument.Load(file))
                {
                    int pageTotal = doc.PageCount;
                    long fileSize = new FileInfo(file).Length;
                    long docId = StaticDocument.BuildId(DOC_TYPE.PNG_OGRINAL, pageTotal, fileSize);
                    long docInfoId = StaticDocument.BuildId(DOC_TYPE.INFO_OGRINAL, pageTotal, fileSize);
                    var sizes = new Dictionary<string, string>();
                    for (int i = 0; i < pageTotal; i++)
                    {
                        byte[] buf = null;
                        string slen = "";
                        bool ok = false;
                        string err = "";
                        try
                        {
                            buf = _pageAsBitmapBytes(doc, i);
                            slen = buf.Length.ToString();
                            ok = redis.HSET(docId, i, buf);
                        }
                        catch (Exception ex)
                        {
                            err = ex.Message + Environment.NewLine + ex.StackTrace;
                        }

                        if (i == 0 && ok)
                        {
                            try
                            {
                                var docInfo = new oDocument()
                                {
                                    id = docInfoId,
                                    file_length = fileSize,
                                    file_name_ascii = "",
                                    file_name_ogrinal = Path.GetFileNameWithoutExtension(file),
                                    file_page = pageTotal,
                                    file_path = file,
                                    file_type = DOC_TYPE.PDF_OGRINAL,
                                    infos = doc.GetInformation().toDictionary(),
                                    metadata = "",
                                    //page_image = buf
                                };
                                var json = Newtonsoft.Json.JsonConvert.SerializeObject(docInfo, Newtonsoft.Json.Formatting.Indented);
                                var bsInfo = Encoding.UTF8.GetBytes(json);
                                var lz = LZ4.LZ4Codec.Wrap(bsInfo);
                                ////var decompressed = LZ4Codec.Unwrap(compressed);
                                redis.HSET("_DOC_INFO", docInfoId.ToString(), lz);
                            }
                            catch(Exception exInfo) {
                                string errInfo = "_DOC_INFO -> " + file + Environment.NewLine + exInfo.Message + Environment.NewLine + exInfo.StackTrace;
                                redis.HSET("_ERROR:PDF2PNG:DOC_INFO", docInfoId.ToString(), errInfo);
                            }
                        }

                        string noti = string.Format("{0}|{1}|{2}|{3}|{4}|{5}", ok ? 1 : 0, docId, i, pageTotal, file, err);
                        redis.PUBLISH("__PDF2PNG_OUT", noti);

                        sizes.Add(string.Format("{0}:{1}", docId, i), slen);
                    }

                    redis.HMSET("_IMG_SIZE", sizes);
                    redis.PUBLISH("__DOC_INFO_OUT", docInfoId.ToString());
                }
            }
        }
        static bool __running = true;

        #endregion

        static void __startApp()
        {
            //File.WriteAllText(@"C:\___.txt", m_port_write.ToString());

            if (m_port_write == 0) m_port_write = 1000;
            if (m_port_read == 0) m_port_read = 1001;
            m_subcriber = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_SUBCRIBE, 1001));
            m_subcriber.PSUBSCRIBE("__PDF2PNG_IN");

            string[] a;
            string s;
            var bs = new List<byte>();
            while (__running)
            {
                if (!m_subcriber.m_stream.DataAvailable)
                {
                    if (bs.Count > 0)
                    {
                        s = Encoding.UTF8.GetString(bs.ToArray()).Trim();
                        bs.Clear();
                        a = s.Split('\r');
                        s = a[a.Length - 1].Trim();
                        if (File.Exists(s))
                        {
                            new Thread(new ParameterizedThreadStart((o)
                                => __createDocumentBackground(o.ToString()))).Start(s);
                        }
                    }

                    Thread.Sleep(100);
                    continue;
                }

                byte b = (byte)m_subcriber.m_stream.ReadByte();
                bs.Add(b);
            }
        }

        static void __stopApp()
        {
            __running = false;
        }


        #region [ SETUP WINDOWS SERVICE ]

        static Thread __threadWS = null;
        static void Main(string[] args)
        {
            if (Environment.UserInteractive)
            {
                StartOnConsoleApp(args);
                Console.WriteLine("Press any key to stop...");
                Console.ReadKey(true);
                Stop();
            }
            else using (var service = new Service())
                    ServiceBase.Run(service);
        }

        public static void StartOnConsoleApp(string[] args) => __startApp();
        public static void StartOnWindowService(string[] args)
        {
            __threadWS = new Thread(new ThreadStart(() => __startApp()));
            __threadWS.IsBackground = true;
            __threadWS.Start();
        }

        public static void Stop()
        {
            __stopApp();
            if (__threadWS != null) __threadWS.Abort();
        }

        #endregion;
    }
}
