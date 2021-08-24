using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarsAudioRecorder2
{
    class DiskWavePlayer : NAudio.Wave.IWavePlayer
    {
        public float Volume { get => 1.0f; set { } }


        private PlaybackState _PlaybackState = PlaybackState.Stopped;
        public PlaybackState PlaybackState => _PlaybackState;
        private object StateLock = new object();


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

            //WaveFormat format = new WaveFormat(_WaveProvider.WaveFormat);

            WaveFormat format = WaveFormat.CreateCustomFormat(WaveFormatEncoding.MpegLayer3, 44100, 1, 128 / 8, 1024, 16);

            //wave = new WaveFileWriter(filename, _WaveProvider.WaveFormat);
            wave = new WaveFileWriter(filename, format);

            Thread thread = new Thread(() =>
            {
                while (_PlaybackState == PlaybackState.Stopped)
                {
                    lock (StateLock)
                    {
                    }

                    Thread.Sleep(100);
                }

                while (_PlaybackState != PlaybackState.Stopped)
                {
                    lock (StateLock)
                    {
                        byte[] buffer = new byte[_WaveProvider.WaveFormat.AverageBytesPerSecond];

                        while (true)
                        {
                            int count = _WaveProvider.Read(buffer, 0, buffer.Length);


                            Console.WriteLine($"filename is {filename}, count is {count}");

                            if (count == 0)
                            {
                                break;
                            }

                            wave.Write(buffer, 0, count);
                        }
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
            lock (StateLock)
            {
                _PlaybackState = PlaybackState.Paused;
            }
        }

        public void Play()
        {
            lock (StateLock)
            {
                _PlaybackState = PlaybackState.Playing;
            }
        }

        public void Stop()
        {
            lock (StateLock)
            {
                _PlaybackState = PlaybackState.Stopped;
            }

            // do we need to raise PlaybackStopped?
        }
    }
}
