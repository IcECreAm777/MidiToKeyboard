using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MidiToKeyboardCore
{
    internal class ConfigurationManager
    {
        #region Properties

        public Dictionary<int, short> ButtonMap { get; set; } = new Dictionary<int, short>();

        #endregion

        #region Methods

        /// <summary>
        /// Loads a csv file and gets the mapped input from there
        /// </summary>
        /// <param name="path"></param>
        public void LoadConfig(string path)
        {
            if (!path.EndsWith("csv")) return;
            using (var reader = new StreamReader(path))
            {
                while (!reader.EndOfStream)
                {
                    var values = reader.ReadLine()?.Split(';');
                    if (values != null) ButtonMap.Add(int.Parse(values[0]), short.Parse(values[1]));
                }
            }
        }

        /// <summary>
        /// Saves the current button configuration as a csv file to the given path.
        /// </summary>
        /// <param name="path"></param>
        public void SaveConfig(string path)
        {
            if(!path.EndsWith("csv")) return;
            using (var writer = new StreamWriter(path))
            {
                foreach (var map in ButtonMap)
                {
                    writer.WriteLine("{0};{1}", map.Key, map.Value);
                }
            }
        }

        #endregion
    }
}
