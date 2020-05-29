﻿using NetCon.model;
using NetCon.repo;
using NetCon.util;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NetCon.viewmodel
{
    class CapturePageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private MainWindowViewModel mMainWindowSharedViewModel;
        private IFrameRepository<Frame> mFramesRepository = FrameRepositoryImpl.instance;

        public CapturePageViewModel(MainWindowViewModel sharedViewModel)
        {
            mMainWindowSharedViewModel = sharedViewModel;

            //Observing subjects
            new SubjectObserver<Frame>(frame =>
            {
                FramesCounter++;
            }).Subscribe(mFramesRepository.FrameSubject);

            new SubjectObserver<CaptureState>(state =>
            {
                if (state is CaptureState.CaptureOn)
                {
                  //  isCapturing = true;
                    mMainWindowSharedViewModel.logAction("Rozpoczęto przechwytywanie ramek");
                }
                else if (state is CaptureState.CaptureOff)
                {
                  //  isCapturing = false;
                    mMainWindowSharedViewModel.logInfo("Zakończono przechwytywanie ramek");
                }
                else if (state is CaptureState.CaptureError)
                {
                    mMainWindowSharedViewModel.logAction(((CaptureState.CaptureError)state).Error.Message);
                  //  isCapturing = false;
                }
            }).Subscribe(mFramesRepository.CaptureState);


            //Setup capture 

            ApplicationConfig config = ConfigFileHandler<ApplicationConfig>.ReadSettings();

            if(config == null)
            {
                mMainWindowSharedViewModel.logAction("Błąd podczas wczytywania ustawień. Załadowano wartości domyślne");
                config = new ApplicationConfig
                {
                    port = 3,
                    bufferSize = 16
                };
            }

            BufferSize = config.bufferSize;
            port = config.port;

            Task.Run(() => mFramesRepository.startCapture());

        }

        private int port = 0;

        public String PortText
        {
            get => port.ToString();
            set
            {
                try
                {
                    port = Int32.Parse(value);
                }
                catch (Exception e){}
            }
        }


        private bool isCapturing = false;

        private ICommand _startButtonCommand;
        public ICommand StartButtonCommand
        {
            get => _startButtonCommand ?? (_startButtonCommand = new CommandHandler(
                    () =>
                    {
                        startCapture();
                    },
                    () => { return !isCapturing; }));
        }

        private ICommand _stopButtonCommand;
        public ICommand StopButtonCommand
        {
            get => _stopButtonCommand ?? (_stopButtonCommand = new CommandHandler(
                    () =>
                    {
                        stopCapture();
                    },
                    () => { return !isCapturing; }));
        }

        private ICommand _saveChangesButtonCommand;
        public ICommand SaveChangesButtonCommand
        {
            get => _saveChangesButtonCommand ?? (_saveChangesButtonCommand = new CommandHandler(
                    () => {
                        ConfigFileHandler<ApplicationConfig>.WriteSettings(new ApplicationConfig
                        {
                            port = this.port,
                            bufferSize = this.BufferSize
                        }
                        );
                        MessageBox.Show(
                            "Zmiany zostaną zastosowane po ponownym uruchomieniu aplikacji",
                            "NetCon v2",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    },
                    () => { return true; }
                    ));
        }


        private void startCapture()
        {
            //TODO przeciążyć start capture o port i rozmiar bufora
            mFramesRepository.resumeCapture();
        }

        private void stopCapture()
        {
            mFramesRepository.pauseCapture();
           // mFramesRepository.stopCapture();
        }



        public int FramesCounter { get; set; } = 0;
        public int BufferSize { get; set; } = 0;
    }
}
