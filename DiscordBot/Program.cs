using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    class Program
    {
        static void Main(string[] args)
        {
            DiscordBot.ConnectClient();
            Console.ReadKey();
            DiscordBot.Client.Dispose();
        }
    }
}
