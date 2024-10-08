﻿using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ConsoleControlAPI;

namespace ConsoleControl.WPF
{
    /// <summary>
    /// Interaction logic for ConsoleControl.xaml
    /// </summary>
    public partial class ConsoleControl : UserControl
    {
        public RichTextBox RichTextBox
        {
            get => RichTextBoxConsole;
            set => RichTextBoxConsole = value;
        }
        
        public SolidColorBrush RichTextBoxBackground
        {
            get => (SolidColorBrush)RichTextBoxConsole.Background;
            set => RichTextBoxConsole.Background = value;
        }
        
        public SolidColorBrush RichTextBoxForeground
        {
            get => (SolidColorBrush)RichTextBoxConsole.Foreground;
            set => RichTextBoxConsole.Foreground = value;
        }
        
        public SolidColorBrush RichTextBoxSelectionBrush
        {
            get => (SolidColorBrush)RichTextBoxConsole.SelectionBrush;
            set => RichTextBoxConsole.SelectionBrush = value;
        }
        
        public SolidColorBrush RichTextBoxCaretBrush
        {
            get => (SolidColorBrush)RichTextBoxConsole.CaretBrush;
            set => RichTextBoxConsole.CaretBrush = value;
        }
        
        public SolidColorBrush RichTextBoxBorderBrush
        {
            get => (SolidColorBrush)RichTextBoxConsole.BorderBrush;
            set => RichTextBoxConsole.BorderBrush = value;
        }
        
        public Thickness RichTextBoxBorderThickness
        {
            get => RichTextBoxConsole.BorderThickness;
            set => RichTextBoxConsole.BorderThickness = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleControl"/> class.
        /// </summary>
        public ConsoleControl()
        {
            InitializeComponent();

            RichTextBox.BorderThickness = new Thickness(0);
            
            //  Handle process events.
            _processInterface.OnProcessOutput += ProcessInterface_OnProcessOutput;
            _processInterface.OnProcessError += ProcessInterface_OnProcessError;
            _processInterface.OnProcessInput += ProcessInterface_OnProcessInput;
            _processInterface.OnProcessExit += ProcessInterface_OnProcessExit;

            //  Wait for key down messages on the rich text box.
            RichTextBoxConsole.PreviewKeyDown += richTextBoxConsole_PreviewKeyDown;
        }

        /// <summary>
        /// Handles the OnProcessError event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void ProcessInterface_OnProcessError(object sender, ProcessEventArgs args)
        {
            //  Write the output, in red
            WriteOutput(args.Content, Colors.Red);

            //  Fire the output event.
            FireProcessOutputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessOutput event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void ProcessInterface_OnProcessOutput(object sender, ProcessEventArgs args)
        {
            Color color = Color.FromArgb(255, 255, 255, 255);

            var content = args.Content.ToLower();

            if (content.Contains(" err "))
                color = Color.FromArgb(255, 255, 0, 0);
            else if (content.Contains(" wrn "))
                color = Color.FromArgb(255, 255, 165, 0);
            else if (args.Content.Contains(" dbg "))
                color = Color.FromArgb(255, 122, 122, 122);
            else if (args.Content.Contains(" vrb "))
                color = Color.FromArgb(255, 156, 156, 156);

            //  Write the output, in white
            WriteOutput(args.Content, color);

            //  Fire the output event.
            FireProcessOutputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessInput event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void ProcessInterface_OnProcessInput(object sender, ProcessEventArgs args)
        {
            FireProcessInputEvent(args);
        }

        /// <summary>
        /// Handles the OnProcessExit event of the processInterace control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void ProcessInterface_OnProcessExit(object sender, ProcessEventArgs args)
        {
            //  Read only again.
            RunOnUIDispatcher(() =>
            {
                //  Are we showing diagnostics?
                if (ShowDiagnostics)
                {
                    WriteOutput($"{Environment.NewLine}{_processInterface.ProcessFileName} exited.", Color.FromArgb(255, 0, 255, 0));
                }

                RichTextBoxConsole.IsReadOnly = true;

                //  And we're no longer running.
                IsProcessRunning = false;
            });
        }

        /// <summary>
        /// Handles the KeyDown event of the richTextBoxConsole control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Input.KeyEventArgs" /> instance containing the event data.</param>
        private void richTextBoxConsole_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var caretPosition = RichTextBoxConsole.GetCaretPosition();
            var delta = caretPosition - _inputStartPos;
            var inReadOnlyZone = delta < 0;

            //  If we're at the input point and it's backspace, bail.
            if (inReadOnlyZone && e.Key == Key.Back)
                e.Handled = true;

            //  Are we in the read-only zone?
            if (inReadOnlyZone)
            {
                //  Allow arrows and Ctrl-C.
                if (!(e.Key == Key.Left ||
                      e.Key == Key.Right ||
                      e.Key == Key.Up ||
                      e.Key == Key.Down ||
                      (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))))
                {
                    e.Handled = true;
                }
            }

            //  Is it the return key?
            if (e.Key == Key.Return)
            {
                //  Get the input.
                var input = new TextRange(RichTextBoxConsole.GetPointerAt(_inputStartPos), RichTextBoxConsole.Selection.Start).Text;

                //  Write the input (without echoing).
                WriteInput(input, Colors.White, false);
            }
        }

        /// <summary>
        /// Writes the output to the console control.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="color">The color.</param>
        public void WriteOutput(string output, Color color)
        {
            if (string.IsNullOrEmpty(_lastInput) == false &&
                (output == _lastInput || output.Replace("\r\n", "") == _lastInput))
                return;

            RunOnUIDispatcher(() =>
            {
                // Ensure the endPointer is correctly positioned at the end of the text.
                var endPointer = RichTextBoxConsole.GetEndPointer();

                // Create a new TextRange starting and ending at the endPointer.
                var range = new TextRange(endPointer, endPointer)
                {
                    Text = output
                };

                // Apply the color to the TextRange.
                range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));

                // Scroll to the end of the RichTextBox to ensure the new text is visible.
                RichTextBoxConsole.ScrollToEnd();

                // Set the caret to the end of the RichTextBox.
                RichTextBoxConsole.SetCaretToEnd();

                // Record the new input start position.
                _inputStartPos = RichTextBoxConsole.GetCaretPosition();
            });
        }

        /// <summary>
        /// Clears the output.
        /// </summary>
        public void ClearOutput()
        {
            RichTextBoxConsole.Document.Blocks.Clear();
            _inputStartPos = 0;
        }

        /// <summary>
        /// Writes the input to the console control.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="color">The color.</param>
        /// <param name="echo">if set to <c>true</c> echo the input.</param>
        public void WriteInput(string input, Color color, bool echo)
        {
            RunOnUIDispatcher(() =>
            {
                //  Are we echoing?
                if (echo)
                {
                    RichTextBoxConsole.Selection.ApplyPropertyValue(TextBlock.ForegroundProperty, new SolidColorBrush(color));
                    RichTextBoxConsole.AppendText(input);
                    _inputStartPos = RichTextBoxConsole.GetEndPosition();
                }

                _lastInput = input;

                //  Write the input.
                _processInterface.WriteInput(input);

                //  Fire the event.
                FireProcessInputEvent(new ProcessEventArgs(input));
            });
        }

        /// <summary>
        /// Runs the on UI dispatcher.
        /// </summary>
        /// <param name="action">The action.</param>
        private void RunOnUIDispatcher(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                //  Invoke the action.
                action();
            }
            else
            {
                Dispatcher.BeginInvoke(action, null);
            }
        }


        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="arguments">The arguments.</param>
        public void StartProcess(string fileName, string arguments)
        {
            StartProcess(new ProcessStartInfo(fileName, arguments));
        }

        /// <summary>
        /// Runs a process.
        /// </summary>
        /// <param name="processStartInfo"><see cref="ProcessStartInfo"/> to pass to the process.</param>
        public void StartProcess(ProcessStartInfo processStartInfo)
        {
            //  Are we showing diagnostics?
            if (ShowDiagnostics)
            {
                WriteOutput($"Preparing to run {processStartInfo.FileName}", Color.FromArgb(255, 0, 255, 0));
                if (!string.IsNullOrEmpty(processStartInfo.Arguments))
                    WriteOutput($" with arguments {processStartInfo.Arguments}.{Environment.NewLine}", Color.FromArgb(255, 0, 255, 0));
                else
                    WriteOutput($".{Environment.NewLine}", Color.FromArgb(255, 0, 255, 0));
            }

            //  Start the process.
            _processInterface.StartProcess(processStartInfo);

            RunOnUIDispatcher(() =>
            {
                //  If we enable input, make the control not read only.
                if (IsInputEnabled)
                    RichTextBoxConsole.IsReadOnly = false;

                //  We're now running.
                IsProcessRunning = true;
            });
        }

        /// <summary>
        /// Stops the process.
        /// </summary>
        public void StopProcess()
        {
            //  Stop the interface.
            _processInterface.StopProcess();
        }

        /// <summary>
        /// Fires the console output event.
        /// </summary>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void FireProcessOutputEvent(ProcessEventArgs args)
        {
            //  Fire the event if it is set.
            OnProcessOutput?.Invoke(this, args);
        }

        /// <summary>
        /// Fires the console input event.
        /// </summary>
        /// <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
        private void FireProcessInputEvent(ProcessEventArgs args)
        {
            //  Fire the event if it is set.
            OnProcessInput?.Invoke(this, args);
        }

        /// <summary>
        /// The internal process interface used to interface with the process.
        /// </summary>
        private readonly ProcessInterface _processInterface = new();

        /// <summary>
        /// Current position that input starts at.
        /// </summary>
        private int _inputStartPos;

        /// <summary>
        /// The last input string (used so that we can make sure we don't echo input twice).
        /// </summary>
        private string _lastInput;

        /// <summary>
        /// Occurs when console output is produced.
        /// </summary>
        public event ProcessEventHandler OnProcessOutput;

        /// <summary>
        /// Occurs when console input is produced.
        /// </summary>
        public event ProcessEventHandler OnProcessInput;

        private static readonly DependencyProperty ShowDiagnosticsProperty =
            DependencyProperty.Register(nameof(ShowDiagnostics), typeof(bool), typeof(ConsoleControl),
                new PropertyMetadata(false, OnShowDiagnosticsChanged));

        /// <summary>
        /// Gets or sets a value indicating whether to show diagnostics.
        /// </summary>
        /// <value>
        ///   <c>true</c> if show diagnostics; otherwise, <c>false</c>.
        /// </value>
        public bool ShowDiagnostics
        {
            get => (bool)GetValue(ShowDiagnosticsProperty);
            set => SetValue(ShowDiagnosticsProperty, value);
        }

        private static void OnShowDiagnosticsChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
        }

        private static readonly DependencyProperty IsInputEnabledProperty =
            DependencyProperty.Register(nameof(IsInputEnabled), typeof(bool), typeof(ConsoleControl),
                new PropertyMetadata(true));

        /// <summary>
        /// Gets or sets a value indicating whether this instance has input enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has input enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsInputEnabled
        {
            get => (bool)GetValue(IsInputEnabledProperty);
            set => SetValue(IsInputEnabledProperty, value);
        }

        internal static readonly DependencyPropertyKey IsProcessRunningPropertyKey =
            DependencyProperty.RegisterReadOnly("IsProcessRunning", typeof(bool), typeof(ConsoleControl),
                new PropertyMetadata(false));

        private static readonly DependencyProperty IsProcessRunningProperty = IsProcessRunningPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets a value indicating whether this instance has a process running.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has a process running; otherwise, <c>false</c>.
        /// </value>
        public bool IsProcessRunning
        {
            get => (bool)GetValue(IsProcessRunningProperty);
            private set => SetValue(IsProcessRunningPropertyKey, value);
        }

        /// <summary>
        /// Gets the internally used process interface.
        /// </summary>
        /// <value>
        /// The process interface.
        /// </value>
        public ProcessInterface ProcessInterface => _processInterface;
    }
}