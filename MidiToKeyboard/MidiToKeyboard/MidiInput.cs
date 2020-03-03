using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Core;

namespace MidiToKeyboardCore
{
    public class MidiInput
    {
        #region fields

        private readonly Dictionary<Keyboard.ScanCodeShort, CancellationTokenSource> _buttonQueue = new Dictionary<Keyboard.ScanCodeShort, CancellationTokenSource>();
        private readonly Keyboard _kbrd = new Keyboard();
        private readonly ConfigurationManager _config = new ConfigurationManager();

        #endregion

        #region Properties

        /// <summary>
        /// The loaded midi device which is used to receive midi events
        /// </summary>
        public InputDevice LoadedDevice { get; set; }

        /// <summary>
        /// A list of all recognized midi devices
        /// </summary>
        public List<InputDevice> MidiDevices { get; private set; } = new List<InputDevice>();

        //TODO raise event when updated
        //TODO enable/ disable types of logging
        /// <summary>
        /// Contains the type and the messages of a log entry with a time stamp. The type can be used for filtering.
        /// </summary>
        public List<Tuple<LogTypeEnum, DateTime, string>> Log { get; } = new List<Tuple<LogTypeEnum, DateTime, string>>();

        #endregion

        #region Constructor

        public MidiInput()
        {
            RefreshMidiDevices();

            //TODO load default config
            
            Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.MESSAGE, DateTime.Now, "MidiInput initialized. Ready to use."));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Searches for Midi Devices and updates the Property
        /// </summary>
        public void RefreshMidiDevices()
        {
            MidiDevices = (List<InputDevice>) InputDevice.GetAll();
            Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.MESSAGE, DateTime.Now, "Midi Devices received."));
        }

        /// <summary>
        /// Loads a midi device by ID
        /// </summary>
        public void InitMidiDevice(int id)
        {
            UnloadMidiDevice();

            try
            {
                LoadedDevice = InputDevice.GetById(id);
                LoadedDevice.EventReceived += OnEventReceived;
                LoadedDevice.StartEventsListening();

                Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.MESSAGE, DateTime.Now, "Midi device loaded and ready to use."));
            }
            catch (Exception e)
            {
                Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.ERROR, DateTime.Now,
                    $"Error loading midi device: {e.Message}"));
            }
        }

        /// <summary>
        /// Loads a midi device by name
        /// </summary>
        public void InitMidiDevice(string name)
        {
            UnloadMidiDevice();

            try
            {
                LoadedDevice = InputDevice.GetByName(name);
                LoadedDevice.EventReceived += OnEventReceived;
                LoadedDevice.StartEventsListening();

                Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.MESSAGE, DateTime.Now, "Midi device loaded and ready to use."));
            }
            catch (Exception e)
            {
                Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.ERROR, DateTime.Now,
                    $"Error loading midi device: {e.Message}"));
            }
        }

        /// <summary>
        /// Unloads the midi device to stop midi inout receiving
        /// </summary>
        public void UnloadMidiDevice()
        {
            if (LoadedDevice != null)
            {
                LoadedDevice.StopEventsListening();
                LoadedDevice.Dispose();
                LoadedDevice = null;
            }
        }

        //PRIVATE

        /// <summary>
        /// Occurs when a midi signal is detected.
        /// It translates midi to keyboard input based on the config file.
        /// </summary>
        private void OnEventReceived(object sender, MidiEventReceivedEventArgs e)
        {
            //Log received event
            var midiDevice = (MidiDevice) sender;
            Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.DEBUG, DateTime.Now, $"Event received from '{midiDevice.Name}': {e.Event}"));
            if (e.Event.EventType != MidiEventType.NoteOn) return;

            //set up cancellation for the pressed key
            var noteOnEvent = e.Event.Clone() as NoteOnEvent;
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            //push events (velocity is how fast or hard a piano key is pressed. if its > 0 then it is going down)
            if (noteOnEvent != null && noteOnEvent.Velocity != 0)
            {
                if (_config.ButtonMap.ContainsKey(noteOnEvent.NoteNumber))
                {
                    SendKey((Keyboard.ScanCodeShort) _config.ButtonMap[noteOnEvent.NoteNumber], token);
                    _buttonQueue.Add((Keyboard.ScanCodeShort) _config.ButtonMap[noteOnEvent.NoteNumber], cts);
                }
            }
            else if(noteOnEvent != null) //up events (velocity 0 only occurs when a piano key is released)
            {
                if (_config.ButtonMap.ContainsKey(noteOnEvent.NoteNumber))
                {
                    RemoveKey((Keyboard.ScanCodeShort) _config.ButtonMap[noteOnEvent.NoteNumber]);
                }
            }
        }

        /// <summary>
        /// Starts sending a keyboard input as key down event to the system
        /// </summary>
        /// <param name="key"></param>
        /// <param name="token"></param>
        private async void SendKey(Keyboard.ScanCodeShort key, CancellationToken token)
        {
            try
            {
                await Task.Run(() => RepeatKey(key, token), token);
            }
            catch (Exception e)
            {
                Log.Add(new Tuple<LogTypeEnum, DateTime, string>(LogTypeEnum.WARNING, DateTime.Now, 
                    $"Something went wrong sending the key. Key event for {key} is stopped. Message: {e.Message}"));
            }
        }

        /// <summary>
        /// Repeats holding down a key until the Task is interrupted
        /// </summary>
        /// <param name="key"></param>
        /// <param name="token"></param>
        private void RepeatKey(Keyboard.ScanCodeShort key, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _kbrd.SendKeyDown(key);
                Thread.Sleep(10);
            }
        }

        /// <summary>
        /// sends a key up event to the system and removes the key from the dictionary
        /// </summary>
        /// <param name="key"></param>
        private void RemoveKey(Keyboard.ScanCodeShort key)
        {
            _buttonQueue[key].Cancel();
            _kbrd.SendKeyUp(key);
            _buttonQueue.Remove(key);
        }

        #endregion
    }
}
