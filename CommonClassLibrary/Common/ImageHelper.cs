using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CommonClassLibrary
{
    public class ImageHelper
    {
        public void SaveImage(string imageUrl, string filename, ImageFormat format = null)
        {
            byte[] imageBytes;
            HttpWebRequest imageRequest = (HttpWebRequest)WebRequest.Create(imageUrl);

            WebResponse imageResponse = imageRequest.GetResponse();
            using (imageResponse)
            {

                Stream responseStream = imageResponse.GetResponseStream();
                using (responseStream)
                {
                    using (BinaryReader br = new BinaryReader(responseStream))
                    {
                        imageBytes = br.ReadBytes(500000);
                        br.Close();
                    }
                    responseStream.Close();
                    imageResponse.Close();
                }
            }
            FileStream fs = new FileStream(filename, FileMode.Create);
            using (fs)
            {
                BinaryWriter bw = new BinaryWriter(fs);
                using (bw)
                    try
                    {
                        bw.Write(imageBytes);
                    }
                    finally
                    {
                        fs.Close();
                        bw.Close();
                    }
            }            
        }
    }
}
