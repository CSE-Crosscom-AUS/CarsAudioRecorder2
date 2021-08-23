using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarsAudioRecorder2b
{
    class DiskWavePlayer : NAudio.Wave.IWavePlayer
    {
        public float Volume { get => 1.0f; set { } }


        private PlaybackState _PlaybackState = PlaybackState.Stopped;
        public PlaybackState PlaybackState => _PlaybackState;


        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        private IWaveProvider _WaveProvider;
        private string filename;
        private NAudio.Wave.WaveFileWriter wave;

        public DiskWavePlayer(string filename)
        {
            this.filename = filename;
        }

        public void Dispose()
        {
            wave.Dispose();
        }

        public void Init(IWaveProvider waveProvider)
        {
            _WaveProvider = waveProvider;
            wave = new WaveFileWriter(filename, _WaveProvider.WaveFormat);

            Thread thread = new Thread(() =>
            {
                while (_PlaybackState == PlaybackState.Stopped)
                {
                    Thread.Sleep(100);
                }

                while (_PlaybackState != PlaybackState.Stopped)
                {
                    byte[] buffer = new byte[_WaveProvider.WaveFormat.AverageBytesPerSecond];

                    while (true)
                    {
                        int count = _WaveProvider.Read(buffer, 0, buffer.Length);
                        

                        Console.WriteLine($"buffer.Length is {buffer.Length}, count is {count}");

                        if (count == 0)
                        {
                            break;
                        }
                        
                        wave.Write(buffer, 0, count);
                    }

                    Thread.Sleep(100);
                }

                wave.Dispose();
            });
            thread.Name = "test thread";
            thread.IsBackground = true;
            thread.Start();
        }

        public void Pause()
        {
            _PlaybackState = PlaybackState.Paused;
        }

        public void Play()
        {
            _PlaybackState = PlaybackState.Playing;
        }

        public void Stop()
        {
            _PlaybackState = PlaybackState.Stopped;

            // do we need to raise PlaybackStopped?
        }
    }
}
