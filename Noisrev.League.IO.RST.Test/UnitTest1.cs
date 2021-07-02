using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Noisrev.League.IO.RST.Test
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            string path = @"C:\Users\Noisr\Downloads\fontconfig_en_us.txt";
            RSTFile rst = new(
                input: File.OpenRead(path),
                leaveOpen: false,
                useLazyLoad: true);
            Console.WriteLine(rst.Entries[0].text is null);
            Console.WriteLine(rst.Entries[0].Text);
        }
    }
}
