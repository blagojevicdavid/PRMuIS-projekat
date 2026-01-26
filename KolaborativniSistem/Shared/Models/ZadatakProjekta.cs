using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Models
{
    [Serializable]
    public class ZadatakProjekta
    {
        public string Naziv { get; set; } = string.Empty;
        public string Zaposleni { get; set; } = string.Empty;

        public StatusZadatka Status { get; set; } = StatusZadatka.NaCekanju;

        public DateTime Rok { get; set; } = DateTime.Now.AddDays(7);

        public int Prioritet { get; set; } = 5;


    }
}
