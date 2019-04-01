﻿
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace MasterConverterGUI
{
    public class UserInfo
    {
        private const string UserDataFileName = "MasterConverterGUI.user";

        public class UserData
        {
            public string Tags { get; set; }
            public string MessagePackDirectory { get; set; }
            public string YamlDirectory { get; set; }
        }

        public UserData Data { get; private set; }

        public UserInfo()
        {
            Data = new UserData();
        }

        public void Load()
        {
            var path = GetUserDataPath();

            if (!File.Exists(path)) { return; }

            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                var text = reader.ReadToEnd();

                Data = JsonConvert.DeserializeObject<UserData>(text);
            }
        }

        public void Save()
        {
            using (var writer = new StreamWriter(@"./" + UserDataFileName, false, Encoding.UTF8))
            {
                var json = JsonConvert.SerializeObject(Data);

                writer.Write(json);
            }
        }

        public string GetUserDataPath()
        {
            return @"./" + UserDataFileName;
        }
    }
}
