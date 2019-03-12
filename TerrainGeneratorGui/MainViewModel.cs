using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using TerrainGeneratorGui.Annotations;

namespace TerrainGeneratorGui
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private double[,] _data;
        private int _progress;
        private int _stage;
        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel()
        {
            GenerateTerrainCommand = new Command(Render);

        }

        private void Render()
        {
            var generator = new Generator2()
            {
                MaximulDeltaAngle = 0.05,
                MinimumHeight = -50,
                MaximumHeight = 300,
            };

            var progress = new Progress<RenderProgress>((p) =>
            {
                if ((int)p.Progress > Progress)
                    Progress = (int)p.Progress;
                if (p.Level <= Stage) return;
                Progress = (int)p.Progress;
                Stage = p.Level;
            });

            Data = generator.CreateTerrain(8, progress);
            Progress = 0;
            Stage =0;
        }

        public double[,] Data
        {
            get => _data;
            set
            {
                _data = value;
                OnPropertyChanged();
            }
        }

        public int Stage
        {
            get { return _stage; }
            set
            {
                if (value == _stage) return;
                _stage = value;
                OnPropertyChanged();
            }
        }

        public int Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
            }
        }

        public ICommand GenerateTerrainCommand { get; }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class Command : ICommand
        {
            private readonly Action _execute;
            private bool _canExecute;
            private readonly Dispatcher gui;
            /// <inheritdoc />
            public bool CanExecute(object parameter)
            {
                return _canExecute;
            }

            /// <inheritdoc />
            public async void Execute(object parameter)
            {
                _canExecute = false;

                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                await Task.Run(() =>
                {
                    _execute?.Invoke();
                });

                _canExecute = true;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }

            public Command(Action execute)
            {
                _execute = execute;
                _canExecute = true;
                gui = Dispatcher.CurrentDispatcher;
            }

            /// <inheritdoc />
            public event EventHandler CanExecuteChanged;
        }
    }
}
