using System.Text;

namespace DebtBot.Extensions
{
    public static class Extensions
    {
        public static async Task<string> ReadToEndAsync(this Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }
        
        public static (int index, string val) WhitespaceLocator(string w)
            => (index: w.IndexOfAny([' ', '\n', '\t', '\v', '\r']), val: w);
    }
}
