﻿using System.Windows;

namespace ConsoleControlSample.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //  Handle certain commands.
            viewModel.StartCommandPromptCommand.Executed += new Apex.MVVM.CommandEventHandler(StartCommandPromptCommand_Executed);
            viewModel.StartNewProcessCommand.Executed += new Apex.MVVM.CommandEventHandler(StartNewProcessCommand_Executed);
            viewModel.StopProcessCommand.Executed += new Apex.MVVM.CommandEventHandler(StopProcessCommand_Executed);
            viewModel.ClearOutputCommand.Executed += new Apex.MVVM.CommandEventHandler(ClearOutputCommand_Executed);
        }

        /// <summary>
        /// Handles the Executed event of the StartCommandPromptCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="Apex.MVVM.CommandEventArgs"/> instance containing the event data.</param>
        private void StartCommandPromptCommand_Executed(object sender, Apex.MVVM.CommandEventArgs args)
        {
            consoleControl.StartProcess("cmd.exe", string.Empty);
            UpdateProcessState();
        }

        /// <summary>
        /// Handles the Executed event of the StartNewProcessCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="Apex.MVVM.CommandEventArgs"/> instance containing the event data.</param>
        private void StartNewProcessCommand_Executed(object sender, Apex.MVVM.CommandEventArgs args)
        {
        }

        /// <summary>
        /// Handles the Executed event of the StopProcessCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="Apex.MVVM.CommandEventArgs"/> instance containing the event data.</param>
        private void StopProcessCommand_Executed(object sender, Apex.MVVM.CommandEventArgs args)
        {
            consoleControl.StopProcess();
            UpdateProcessState();
        }

        /// <summary>
        /// Handles the Executed event of the ClearOutputCommand control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="args">The <see cref="Apex.MVVM.CommandEventArgs"/> instance containing the event data.</param>
        private void ClearOutputCommand_Executed(object sender, Apex.MVVM.CommandEventArgs args)
        {
            consoleControl.ClearOutput();
        }

        private void UpdateProcessState()
        {
            //  Update the state.
            if (consoleControl.IsProcessRunning)
                viewModel.ProcessState = $"Running {System.IO.Path.GetFileName(consoleControl.ProcessInterface.ProcessFileName)}";
            else
                viewModel.ProcessState = "Not Running";
        }
    }
}