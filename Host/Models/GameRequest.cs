using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Host.Models
{
    public class GameRequest
    {
        public string Connection {get;set;}
        public string Token { get; set; }
        public bool IsComputerGame { get; set; }
        public bool IsPublicGame
        {
            get
            {
                return String.IsNullOrEmpty(Token);
            }
        }
    }
}
