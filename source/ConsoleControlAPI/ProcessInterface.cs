using System;
using System.Text;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;

namespace ConsoleControlAPI
{
    /// <summary>
    /// A ProcessEventHandler is a delegate for process input/output events.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
    public delegate void ProcessEventHandler(object sender, ProcessEventArgs args);

    /// <summary>
    /// A class the wraps a process, allowing programmatic input and output.
    /// </summary>
    public class ProcessInterface : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessInterface"/> class.
        /// </summary>
        public ProcessInterface()
        {
            //  Configure the output worker.
            _outputWorker.WorkerReportsProgress = true;
            _outputWorker.WorkerSupportsCancellation = true;
            _outputWorker.DoWork += outputWorker_DoWork;
            _outputWorker.ProgressChanged += outputWorker_ProgressChanged;

            //  Configure the error worker.
            _errorWorker.WorkerReportsProgress = true;
            _errorWorker.WorkerSupportsCancellation = true;
            _errorWorker.DoWork += errorWorker_DoWork;
            _errorWorker.ProgressChanged += errorWorker_ProgressChanged;
        }

        /// <summary>
        /// Handles the ProgressChanged event of the outputWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void outputWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //  We must be passed a string in the user state.
            if (e.UserState is string state)
            {
                //  Fire the output event.
                FireProcessOutputEvent(state);
            }
        }

        /// <summary>
        /// Handles the DoWork event of the outputWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void outputWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (_outputWorker.CancellationPending == false)
            {
                //  Any lines to read?
                int count;
                var buffer = new char[1024];
                do
                {
                    var builder = new StringBuilder();
                    count = _outputReader.Read(buffer, 0, 1024);
                    builder.Append(buffer, 0, count);
                    _outputWorker.ReportProgress(0, builder.ToString());
                } while (count > 0);

                System.Threading.Thread.Sleep(200);
            }
        }

        /// <summary>
        /// Handles the ProgressChanged event of the errorWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        private void errorWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //  The userstate must be a string.
            if (e.UserState is string state)
            {
                //  Fire the error event.
                FireProcessErrorEvent(state);
            }
        }

        /// <summary>
        /// Handles the DoWork event of the errorWorker control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        private void errorWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            while (_errorWorker.CancellationPending == false)
            {
                //  Any lines to read?
                int count;
                var buffer = new char[1024];
                do
                {
                    var builder = new StringBuilder();
                    count = _errorReader.Read(buffer, 0, 1024);
                    builder.Append(buffer, 0, count);
                    _errorWorker.ReportProgress(0, builder.ToString());
                } while (count > 0);

                System.Threading.Thread.Sleep(200);
            }
        }

        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="arguments">The arguments.</param>
        public void StartProcess(string fileName, string arguments)
        {
            //  Create the process start info.
            var processStartInfo = new ProcessStartInfo(fileName, arguments);
            StartProcess(processStartInfo);
        }

        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="processStartInfo"><see cref="ProcessStartInfo"/> to pass to the process.</param>
        public void StartProcess(ProcessStartInfo processStartInfo)
        {
            //  Set the options.
            processStartInfo.UseShellExecute = false;
            processStartInfo.ErrorDialog = false;
            processStartInfo.CreateNoWindow = true;

            //  Specify redirection.
            processStartInfo.RedirectStandardError = true;
            processStartInfo.RedirectStandardInput = true;
            processStartInfo.RedirectStandardOutput = true;

            //  Create the process.
            _process = new Process();
            _process.EnableRaisingEvents = true;
            _process.StartInfo = processStartInfo;
            _process.Exited += currentProcess_Exited;

            //  Start the process.
            try
            {
                _process.Start();
            }
            catch (Exception e)
            {
                //  Trace the exception.
                Trace.WriteLine("Failed to start process " + processStartInfo.FileName + " with arguments '" + processStartInfo.Arguments + "'");
                Trace.WriteLine(e.ToString());
                return;
            }

            //  Store name and arguments.
            _processFileName = processStartInfo.FileName;
            _processArguments = processStartInfo.Arguments;

            //  Create the readers and writers.
            _inputWriter = _process.StandardInput;
            _outputReader = TextReader.Synchronized(_process.StandardOutput);
            _errorReader = TextReader.Synchronized(_process.StandardError);

            //  Run the workers that read output and error.
            _outputWorker.RunWorkerAsync();
            _errorWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Stops the process.
        /// </summary>
        public void StopProcess()
        {
            //  Handle the trivial case.
            if (IsProcessRunning == false)
                return;

            //  Kill the process.
            _process.Kill();
        }

        /// <summary>
        /// Handles the Exited event of the currentProcess control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void currentProcess_Exited(object sender, EventArgs e)
        {
            //  Fire process exited.
            FireProcessExitEvent(_process.ExitCode);

            //  Disable the threads.
            _outputWorker.CancelAsync();
            _errorWorker.CancelAsync();
            _inputWriter = null;
            _outputReader = null;
            _errorReader = null;
            _process = null;
            _processFileName = null;
            _processArguments = null;
        }

        /// <summary>
        /// Fires the process output event.
        /// </summary>
        /// <param name="content">The content.</param>
        private void FireProcessOutputEvent(string content)
        {
            //  Get the event and fire it.
            var theEvent = OnProcessOutput;
            theEvent?.Invoke(this, new ProcessEventArgs(content));
        }

        /// <summary>
        /// Fires the process error output event.
        /// </summary>
        /// <param name="content">The content.</param>
        private void FireProcessErrorEvent(string content)
        {
            //  Get the event and fire it.
            var theEvent = OnProcessError;
            theEvent?.Invoke(this, new ProcessEventArgs(content));
        }

        /// <summary>
        /// Fires the process input event.
        /// </summary>
        /// <param name="content">The content.</param>
        private void FireProcessInputEvent(string content)
        {
            //  Get the event and fire it.
            var theEvent = OnProcessInput;
            theEvent?.Invoke(this, new ProcessEventArgs(content));
        }

        /// <summary>
        /// Fires the process exit event.
        /// </summary>
        /// <param name="code">The code.</param>
        private void FireProcessExitEvent(int code)
        {
            //  Get the event and fire it.
            var theEvent = OnProcessExit;
            theEvent?.Invoke(this, new ProcessEventArgs(code));
        }

        /// <summary>
        /// Writes the input.
        /// </summary>
        /// <param name="input">The input.</param>
        public void WriteInput(string input)
        {
            if (IsProcessRunning)
            {
                _inputWriter.WriteLine(input);
                _inputWriter.Flush();
            }
        }

        /// <summary>Finalizes an instance of the <see cref="ProcessInterface"/> class.</summary>
        ~ProcessInterface()
        {
            Dispose(true);
        }

        /// <summary>Releases unmanaged and - optionally - managed resources.</summary>
        /// <param name="native">
        ///   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected void Dispose(bool native)
        {
            if (_outputWorker != null)
            {
                _outputWorker.Dispose();
                _outputWorker = null;
            }

            if (_errorWorker != null)
            {
                _errorWorker.Dispose();
                _errorWorker = null;
            }

            if (_process != null)
            {
                _process.Dispose();
                _process = null;
            }

            if (_inputWriter != null)
            {
                _inputWriter.Dispose();
                _inputWriter = null;
            }

            if (_outputReader != null)
            {
                _outputReader.Dispose();
                _outputReader = null;
            }

            if (_errorReader != null)
            {
                _errorReader.Dispose();
                _errorReader = null;
            }
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The current process.
        /// </summary>
        private Process _process;

        /// <summary>
        /// The input writer.
        /// </summary>
        private StreamWriter _inputWriter;

        /// <summary>
        /// The output reader.
        /// </summary>
        private TextReader _outputReader;

        /// <summary>
        /// The error reader.
        /// </summary>
        private TextReader _errorReader;

        /// <summary>
        /// The output worker.
        /// </summary>
        private BackgroundWorker _outputWorker = new();

        /// <summary>
        /// The error worker.
        /// </summary>
        private BackgroundWorker _errorWorker = new();

        /// <summary>
        /// Current process file name.
        /// </summary>
        private string _processFileName;

        /// <summary>
        /// Arguments sent to the current process.
        /// </summary>
        private string _processArguments;

        /// <summary>
        /// Occurs when process output is produced.
        /// </summary>
        public event ProcessEventHandler OnProcessOutput;

        /// <summary>
        /// Occurs when process error output is produced.
        /// </summary>
        public event ProcessEventHandler OnProcessError;

        /// <summary>
        /// Occurs when process input is produced.
        /// </summary>
        public event ProcessEventHandler OnProcessInput;

        /// <summary>
        /// Occurs when the process ends.
        /// </summary>
        public event ProcessEventHandler OnProcessExit;

        /// <summary>
        /// Gets a value indicating whether this instance is process running.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is process running; otherwise, <c>false</c>.
        /// </value>
        public bool IsProcessRunning
        {
            get
            {
                try
                {
                    return (_process != null && _process.HasExited == false);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets the internal process.
        /// </summary>
        public Process Process => _process;

        /// <summary>
        /// Gets the name of the process.
        /// </summary>
        /// <value>
        /// The name of the process.
        /// </value>
        public string ProcessFileName => _processFileName;

        /// <summary>
        /// Gets the process arguments.
        /// </summary>
        public string ProcessArguments => _processArguments;
    }
}