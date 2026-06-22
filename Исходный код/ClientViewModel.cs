using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyFirstCRM
{
    public class ClientViewModel
    {
        public int DisplayId { get; set; } // Порядковый номер для отображения
        public int RealId { get; set; }    // Реальный ID из базы данных
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; } = "";
    }
}


