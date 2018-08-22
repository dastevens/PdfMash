using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PdfMash
{
    class Facture
    {
        public Facture(int pageNumber, string pageContent)
        {
            PageNumber = pageNumber;
            Pages.Add(pageContent);
        }

        public void AppendPage(string page)
        {
            Pages.Add(page);
        }
        List<string> Pages { get; } = new List<string>();

        public int PageNumber { get; }
        public int PageCount { get { return Pages.Count; } }
        public string FileName()
        {
            //FACTURE - ORIGINAL No 18009816 DU 03 / 07 / 18
            //AVOIR - ORIGINAL No 18009816 DU 03 / 07 / 18
            var factureLine = Pages.First().Split($"{Environment.NewLine}".ToCharArray())
                .Where(line => line.StartsWith("FACTURE - ORIGINAL No ") || line.StartsWith("AVOIR - ORIGINAL No "))
                .SingleOrDefault();
            var isAvoir = factureLine.StartsWith("AVOIR");
            var factureNumber = Regex.Match(factureLine, "\\d{8}").Value;
            var factureDateString = Regex.Match(factureLine, "\\d{2}/\\d{2}/\\d{2}").Value;
            var factureDate = DateTime.ParseExact(factureDateString, "dd/MM/yy", System.Globalization.CultureInfo.InvariantCulture);

            // Look for figure like XXXX EUR at end of line on last page
            var netAPayerString = Pages.Last()
                .Split($"{Environment.NewLine}".ToCharArray())
                .Reverse()
                .Where(line => Regex.IsMatch(line, "-?\\d+( \\d{3})*\\.\\d\\d EUR"))
                .Select(line => Regex.Match(line, "-?\\d+( \\d{3})*\\.\\d\\d")?.Value)
                .First()
                .Replace(" ", "");
            var netAPayer = decimal.Parse(netAPayerString, System.Globalization.CultureInfo.InvariantCulture).ToString("C");
            return $"{factureDate.ToString("yyyy-MM-dd")} CB {factureNumber} - {(isAvoir ? "(avoir) " : "")}{netAPayer.Substring(0, netAPayer.Length - 2)} EUR.pdf";
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var inputPdf = args[0];

            var factures = FindFactures(inputPdf).ToList();

            foreach (var facture in factures)
            {
                Console.WriteLine($"{facture.PageNumber}: {facture.FileName()}");
            }
            SplitFactures(inputPdf, factures);
        }

        static void SplitFactures(string inputPdf, IEnumerable<Facture> factures)
        {
            var folder = System.IO.Path.GetDirectoryName(inputPdf);
            var document = IronPdf.PdfDocument.FromFile(inputPdf);
            foreach (var facture in factures)
            {
                var from = facture.PageNumber;
                var to = facture.PageNumber + facture.PageCount - 1;
                var filename = facture.FileName();
                var split = document.CopyPages(from, to);
                split.SaveAs(System.IO.Path.Combine(folder, filename));
            }
        }

        static IEnumerable<Facture> FindFactures(string inputPdf)
        {
            string pattern = "Page: 1";
            using (var reader = new iTextSharp.text.pdf.PdfReader(inputPdf))
            {
                Facture facture = null;
                for (var page = 0; page < reader.NumberOfPages; page++)
                {
                    var pageContent = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, page + 1);
                    if (pageContent.Contains(pattern))
                    {
                        if (facture != null)
                        {
                            yield return facture;
                        }
                        facture = new Facture(page, pageContent);
                    }
                    else
                    {
                        facture.AppendPage(pageContent);
                    }
                }
                if (facture != null)
                {
                    yield return facture;
                }
            }
        }
    }
}
