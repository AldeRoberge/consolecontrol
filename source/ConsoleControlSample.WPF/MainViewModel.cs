﻿using Apex.MVVM;

namespace ConsoleControlSample.WPF
{
    /// <summary>
    /// The Main ViewModel.
    /// </summary>
    public class MainViewModel : ViewModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        public MainViewModel()
        {
            StartCommandPromptCommand = new Command(() => { });
            StartNewProcessCommand = new Command(() => { });
            StopProcessCommand = new Command(() => { });
            ClearOutputCommand = new Command(() => { });
        }

        private NotifyingProperty _processStateProperty = new("ProcessState", typeof(string), default(string));

        /// <summary>
        /// Gets or sets the state of the process.
        /// </summary>
        /// <value>
        /// The state of the process.
        /// </value>
        public string ProcessState
        {
            get => (string)GetValue(_processStateProperty);
            set => SetValue(_processStateProperty, value);
        }

        /// <summary>
        /// Gets the start command prompt command.
        /// </summary>
        public Command StartCommandPromptCommand { get; private set; }

        /// <summary>
        /// Gets or sets the start new process command.
        /// </summary>
        /// <value>
        /// The start new process command.
        /// </value>
        public Command StartNewProcessCommand { get; private set; }

        /// <summary>
        /// Gets or sets the stop process command.
        /// </summary>
        /// <value>
        /// The stop process command.
        /// </value>
        public Command StopProcessCommand { get; private set; }

        /// <summary>
        /// Gets the clear output command.
        /// </summary>
        public Command ClearOutputCommand { get; private set; }
    }
}