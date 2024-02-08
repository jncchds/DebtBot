using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DebtBot.Extensions
{
    public static class StreamExtensions
    {
        public static async Task<string> ReadToEndAsync(this Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
