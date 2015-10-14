using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Speech.Recognition;
using OneNoteDictationTool.Model;

namespace OneNoteDictationTool
{
    public partial class FormMain : Form // TODO: change this from form 
    {
        public FormMain()
        {
            ShowInTaskbar = false;
            InitializeComponent();
            Hide();
            notifyIconMain.ContextMenu = CmMain;

            try
            {
                InitializeSpeechRecognition();
            }
            catch (Exception)
            {
                MessageBox.Show("Speech recognition failed. Application exiting.");
                Environment.Exit(1);
            }
        }

        #region Private Methods
        private void InitializeSpeechRecognition()
        {
            //SpeechRecognitionEngine.SpeechRecognized += SpeechRecognition_SpeechRecognized;
            SpeechRecognitionEngine.SpeechRecognized += SpeechRecognition_DictationSpeechRecognized;
            //LoadCommands();
            LoadDictationCommands();
            SpeechRecognitionEngine.SetInputToDefaultAudioDevice();

            Closing += UnhookEvents_OnClosing;

            SpeechRecognitionEngine.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void LoadDictationCommands()
        {
            // The default dictation grammar by Windows Desktop speech
            var defDictationGrammar = new DictationGrammar
            {
                Name = "default",
                Enabled = true
            };

            // Spelling dictation grammar
            var spellingGrammar = new DictationGrammar("grammar:dictation#spelling")
            {
                Name = "spelling dictation",
                Enabled = true
            };

            // TODO: choices should compare against a dictionary so that the spoken word and related exe don't need to be the same
            var cmdChoices = new Choices("OneNote", "Notepad");
            var grammarBuilder = new GrammarBuilder("start");
            grammarBuilder.Append(cmdChoices);
            var cmdGrammar = new Grammar(grammarBuilder);

            SpeechRecognitionEngine.LoadGrammar(defDictationGrammar);
            SpeechRecognitionEngine.LoadGrammar(spellingGrammar);
            SpeechRecognitionEngine.LoadGrammar(cmdGrammar);
        }

        private void LoadCommands()
        {
            var choices = new Choices();
            foreach (var key in _defaultCommands.Keys)
                choices.Add(key);

            var grammar = new Grammar(new GrammarBuilder(choices));
            SpeechRecognitionEngine.LoadGrammar(grammar);
        }

        private string GetKnownCommand(string pCommandText)
        {
            try
            {
                var cmd = Commands.FirstOrDefault(c => c.RecognizedText.Equals(pCommandText));
                if (cmd != null)
                {
                    // Check if process is already running
                    var shellCmd = cmd.ShellCommand;
                    if (shellCmd.Contains(".exe"))
                    {
                        var procName = shellCmd.Split(new[] { ".exe" }, StringSplitOptions.None)[0];
                        if (Process.GetProcessesByName(procName).Length != 0)
                            return $"Process already running: {cmd.ShellCommand}";
                    }

                    Process.Start(cmd.ShellCommand);
                    return $"Process started: {cmd.ShellCommand}";
                }
            }
            catch (Exception)
            {
                return $"Unable to start process: {pCommandText}";
            }

            return pCommandText;
        }

        private SpeechRecognitionEngine CreateSpeechRecognitionEngine(string pPreferredCulture = null)
        {
            /* TODO: doing it this way to account for different cultures
             * -- using default or now */
            return new SpeechRecognitionEngine(SpeechRecognitionEngine.InstalledRecognizers()[0]);
        }
        #endregion Private Methods

        #region Events
        #region Speech Events
        private void SpeechRecognition_DictationSpeechRecognized(object s, SpeechRecognizedEventArgs e)
        {
            var result = e.Result.Text.ToLower();

            if (result.Equals("exit all"))
            {
                Close();
                return;
            }

            if (result.Equals("view commands"))
            {
                ShowCommands();
                return;
            }

            if (result.Equals("close commands"))
            {
                if (_cmdForm != null && !_cmdForm.IsDisposed && _cmdForm.Visible)
                    _cmdForm.Close();
                return;
            }

            if (result.Equals("view history"))
            {
                ShowLog();
                return;
            }

            if (result.Equals("close history"))
            {
                if (_logForm != null && !_logForm.IsDisposed && _logForm.Visible)
                    _logForm.Close();
                return;
            }

            if (result.StartsWith("start"))
            {
                if (result.Equals("start all"))
                {
                    _startDictation = true;
                    _logStrings.Add($"{CurrentTime}: Dictation started");
                    return;
                }

                result = result.Split(new[] { "start" }, StringSplitOptions.None)[1].ToLower().Trim();
                if (!result.EndsWith(".exe"))
                    result = result + ".exe";
                try
                {
                    Process.Start(result);
                    _logStrings.Add($"{CurrentTime}: Started process: {result}");
                    return;
                }
                catch (Exception)
                {
                    _logStrings.Add($"{CurrentTime}: Unable to start process: {result}");
                    return;
                }
            }

            if (!_startDictation) return;
            // Otherwise, not a command - send text to output
            _logStrings.Add($"{CurrentTime}: Dictated: {result}");
            SendKeys.SendWait(" " + e.Result.Text);
        }

        private void SpeechRecognition_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            var msgToLog = GetKnownCommand(e.Result.Text);
            _logStrings.Add($"{DateTime.Now.TimeOfDay}: {msgToLog}");
        }
        #endregion Speech Events

        #region Form Events
        private void FormMain_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Minimized;
        }

        private void UnhookEvents_OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            SpeechRecognitionEngine.RecognizeAsyncStop();
            SpeechRecognitionEngine.Dispose();
        }
        #endregion Form Events
        #endregion Events

        #region Fields/Properties

        private bool _startDictation;

        private string CurrentTime => DateTime.Now.ToShortTimeString();

        private SpeechRecognitionEngine _speechRecognitionEngine = null;
        private SpeechRecognitionEngine SpeechRecognitionEngine => _speechRecognitionEngine ?? (_speechRecognitionEngine = CreateSpeechRecognitionEngine());

        private Dictionary<string, string> _defaultCommands = new Dictionary<string, string>
        {
            { "one note", "onenote.exe"},
            // TODO: add more, eventually will be a config file
        };

        private List<Command> _commands;
        private List<Command> Commands
        {
            get
            {
                if (_commands != null) return _commands;

                _commands = new List<Command>
                {
                    new Command
                    {
                        RecognizedText = "one note",
                        ShellCommand = "onenote.exe"
                    }
                };
                return _commands;
            }
        }

        private CommandForm _cmdForm = new CommandForm();
        private LogForm _logForm = new LogForm();
        private List<string> _logStrings = new List<string>();

        private ContextMenu _cmMain;
        private ContextMenu CmMain
        {
            get
            {
                if (_cmMain != null) return _cmMain;

                var logItem = new MenuItem { Text = "View Log" };
                logItem.Click += (o, s) =>
                {
                    ShowLog();
                };
                var cmdItem = new MenuItem { Text = "View Commands" };
                cmdItem.Click += CmdItemOnClick;

                var exitItem = new MenuItem { Text = "Exit" };
                exitItem.Click += (o, s) => { Close(); };
                _cmMain = new ContextMenu(new[] { cmdItem, logItem, exitItem });

                return _cmMain;
            }
        }

        private void CmdItemOnClick(object sender, EventArgs eventArgs)
        {
            ShowCommands();
        }

        private void ShowCommands()
        {
            if (_cmdForm == null || _cmdForm.IsDisposed)
                _cmdForm = new CommandForm();
            if (!_cmdForm.Visible)
                _cmdForm.Show();
        }

        private void ShowLog()
        {
            if (_logForm == null || _logForm.IsDisposed)
                _logForm = new LogForm();
            _logForm.UpdateLogMessages(_logStrings);
            if (!_logForm.Visible)
                _logForm.Show();
        }

        #endregion
    }
}
