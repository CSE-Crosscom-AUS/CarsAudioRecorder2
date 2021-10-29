using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarsAudioRecorder2
{
    public class CarsAudioRecorderSettings
    {
        [JsonIgnore]
        public static string SettingsPath => System.IO.Path.Combine(Program.GetSettingsFolder(), "settings.json");


        public int ChannelCount { get; set; } = 4;



        public bool Validate()
        {
            bool ok = true;

            if (ChannelCount < 1 || ChannelCount > 4)
            {
                Console.WriteLine("ChannelCount must be between 1 and 4");
                ok = false;
            }

            return ok;
        }


        public void Save()
        {
            string j = JsonConvert.SerializeObject(this, Formatting.Indented);

            System.IO.File.WriteAllText(SettingsPath, j);
        }


        public void Load()
        {
            if (System.IO.File.Exists(SettingsPath))
            {
                string j = System.IO.File.ReadAllText(SettingsPath);

                JsonConvert.PopulateObject(j, this);
            }

            Save();
        }
    }
}