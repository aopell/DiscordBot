﻿using System;
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
            DiscordTools.ConnectClient();
            Console.ReadKey();
            DiscordTools.Client.Dispose();
        }
    }
}
