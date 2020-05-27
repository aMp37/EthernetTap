using NetCon.model;
using NetCon.repo;
using NetCon.util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace NetCon.viewmodel
{
    class CapturePageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private MainWindowViewModel mMainWindowSharedViewModel;
        private int bufferSize = 16;
        private int port = 0;
        private IFrameRepository<Frame> mFramesRepository = FrameRepositoryImpl.instance;

        public bool IsCapturing { get; set; }
        public int Port { get; set; }
        public int BufferSize{get;set;}
        public int FramesCounter { get; set; } = 0;

        public CapturePageViewModel(MainWindowViewModel sharedViewModel)
        {
            mMainWindowSharedViewModel = sharedViewModel;

            mFramesRepository.applyListeningConfiguration(port, bufferSize);
            Task.Run(() => mFramesRepository.startFramesListening());

            new SubjectObserver<Frame>(frame =>
            {
                FramesCounter++;
            }).Subscribe(mFramesRepository.FrameSubject);

            new SubjectObserver<CaptureState>(state =>
            {
                if (state is CaptureState.CaptureOn)
                {
                    IsCapturing = true;
                    mMainWindowSharedViewModel.logAction($"Rozpoczęto przechwytywanie ramek");
                }
                else if (state is CaptureState.CaptureOff)
                {
                    IsCapturing = false;
                    mMainWindowSharedViewModel.logInfo($"Zakończono przechwytywanie ramek");
                }
                else if (state is CaptureState.ListeningOn)
                {
                    mMainWindowSharedViewModel.logInfo($"Uruchomiono wątek nasłuchujący ruch sieciowy z urządzenia na porcie {port}");
                }
                else if (state is CaptureState.ListeningOff)
                {
                    mMainWindowSharedViewModel.logInfo($"Zakończono wątek nasłuchujący ruch sieciowy z urządzenia na porcie {port}");
                }
                else if (state is CaptureState.CaptureError)
                {
                    mMainWindowSharedViewModel.logAction(((CaptureState.CaptureError)state).Error.Message);
                  //  isCapturing = false;
                }
            }).Subscribe(mFramesRepository.CaptureState);
        }

        public String PortText
        {
            get
            {
                return port.ToString();
            }

            set
            {
                try
                {
                    port = Int32.Parse(value);
                }
                catch (Exception e)
                {
                    //TODO obsługa błędów
                }

            }
        }

        private ICommand _startButtonCommand;
        public ICommand StartButtonCommand
        {
            get
            {
                return _startButtonCommand ?? (_startButtonCommand = new CommandHandler(
                    () =>
                    {
                        mFramesRepository.startCapture();
                    },
                    () => { return !IsCapturing; }));
            }
        }

        private ICommand _stopButtonCommand;
        public ICommand StopButtonCommand
        {
            get
            {
                return _stopButtonCommand ?? (_stopButtonCommand = new CommandHandler(
                    () =>
                    {
                        mFramesRepository.stopCapture();
                    },
                    () => { return IsCapturing; }));
            }
        }
    }
}
