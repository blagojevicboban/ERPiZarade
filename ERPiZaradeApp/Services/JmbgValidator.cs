using System;
using System.Linq;

namespace ERPiZaradeApp.Services
{
    public static class JmbgValidator
    {
        public static bool Validate(string? jmbg, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(jmbg))
            {
                error = "JMBG ne može biti prazan.";
                return false;
            }

            jmbg = jmbg.Trim();
            if (jmbg.Length != 13)
            {
                error = "JMBG mora imati tačno 13 cifara.";
                return false;
            }

            if (!jmbg.All(char.IsDigit))
            {
                error = "JMBG se mora sastojati isključivo od cifara.";
                return false;
            }

            // Provera datuma rođenja
            int dan = int.Parse(jmbg.Substring(0, 2));
            int mesec = int.Parse(jmbg.Substring(2, 2));
            int godinaSufiks = int.Parse(jmbg.Substring(4, 3));

            int godina = 1000 + godinaSufiks;
            if (godinaSufiks < 800)
            {
                godina = 2000 + godinaSufiks;
            }

            try
            {
                var dt = new DateTime(godina, mesec, dan);
                if (dt > DateTime.Now)
                {
                    error = "Datum rođenja iz JMBG-a ne može biti u budućnosti.";
                    return false;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                error = $"Datum rođenja iz JMBG-a ({dan:D2}.{mesec:D2}.{godina}.) nije validan kalendarski datum.";
                return false;
            }

            // Kontrolna cifra po modulu 11
            int d1 = jmbg[0] - '0';
            int d2 = jmbg[1] - '0';
            int d3 = jmbg[2] - '0';
            int d4 = jmbg[3] - '0';
            int d5 = jmbg[4] - '0';
            int d6 = jmbg[5] - '0';
            int d7 = jmbg[6] - '0';
            int d8 = jmbg[7] - '0';
            int d9 = jmbg[8] - '0';
            int d10 = jmbg[9] - '0';
            int d11 = jmbg[10] - '0';
            int d12 = jmbg[11] - '0';
            int d13 = jmbg[12] - '0';

            int suma = 7 * (d1 + d7) + 6 * (d2 + d8) + 5 * (d3 + d9) + 4 * (d4 + d10) + 3 * (d5 + d11) + 2 * (d6 + d12);
            int ostatak = suma % 11;
            int k = 11 - ostatak;

            if (ostatak == 0)
            {
                k = 0;
            }
            else if (ostatak == 1)
            {
                error = "Kontrolna cifra JMBG-a nije ispravna (ostatak po modulu 11 je 1, što je nevažeća kombinacija).";
                return false;
            }

            if (d13 != k)
            {
                error = $"Neispravan JMBG (kontrolna cifra je {d13}, a po modulu 11 bi trebalo da bude {k}).";
                return false;
            }

            return true;
        }
    }
}
