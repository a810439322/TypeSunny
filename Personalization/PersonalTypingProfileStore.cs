using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TypeSunny.Personalization
{
    internal sealed class PersonalTypingProfileStore
    {
        private readonly string path;

        public PersonalTypingProfileStore()
            : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "预测日志", "PersonalTypingProfile.json"))
        {
        }

        public PersonalTypingProfileStore(string path)
        {
            this.path = path;
        }

        public PersonalTypingProfile Load()
        {
            try
            {
                if (!File.Exists(path))
                    return new PersonalTypingProfile();

                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                    return new PersonalTypingProfile();

                PersonalTypingProfile profile = JsonConvert.DeserializeObject<PersonalTypingProfile>(json);
                return profile ?? new PersonalTypingProfile();
            }
            catch
            {
                return new PersonalTypingProfile();
            }
        }

        public void Save(PersonalTypingProfile profile)
        {
            if (profile == null)
                return;

            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var settings = new JsonSerializerSettings { Formatting = Formatting.None };
                string json = JsonConvert.SerializeObject(profile, settings);
                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }
}
