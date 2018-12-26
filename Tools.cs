using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Verdant
{
    public static class Tools
    {
        public static BitmapImage UrlToXamlImage(string url)
        {
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(url, UriKind.Absolute);
            bi.EndInit();
            return bi;
        }
    }
}
