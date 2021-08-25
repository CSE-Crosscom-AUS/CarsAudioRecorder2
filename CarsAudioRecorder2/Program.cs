using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarsAudioRecorder2
{
    class Program
    {
        static void Main(string[] args)
        {
            NAudio.CoreAudioApi.MMDevice InputDevice = GetMMDevice();


            if (InputDevice != null)
            {
                Record(InputDevice);
            }
        }


        static NAudio.CoreAudioApi.MMDevice GetMMDevice()
        {
            // Behringer drivers are required
            // I used "UMC Driver 5.12.0 (for Windows 10)"
            // from https://www.behringer.com/product.html?modelCode=P0BK1



            NAudio.CoreAudioApi.MMDevice InputDevice = null;


            using (NAudio.CoreAudioApi.MMDeviceEnumerator enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator())
            {
                foreach (NAudio.CoreAudioApi.MMDevice device in enumerator.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.DeviceState.All))
                {
                    Console.Write("{0}, {1}", device.FriendlyName, device.State);

                    if (InputDevice == null)
                    {
                        if (device.FriendlyName.Contains("BEHRINGER UMC 404HD") && device.FriendlyName.Contains("1-4"))
                        {
                            Console.Write("!!!");
                            InputDevice = device;
                        }
                    }

                    Console.WriteLine();
                }
            }

            return InputDevice;
        }


        public static void Record(NAudio.CoreAudioApi.MMDevice InputDevice)
        {

            short[] silence = new short[2000];

            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice, false, 1000))
            {
                Concentus.Oggfile.OpusOggWriteStream[] oggfiles = new Concentus.Oggfile.OpusOggWriteStream[4];

                for (int i = 0; i < oggfiles.Length; i++)
                {
                    oggfiles[i] = CreateOgg($"out{i}.opus");
                }



                NAudio.Wave.BufferedWaveProvider[] bwp = new NAudio.Wave.BufferedWaveProvider[4];

                for (int i = 0; i < bwp.Length; i++)
                {
                    bwp[i] = new NAudio.Wave.BufferedWaveProvider(capture.WaveFormat);
                    bwp[i].BufferDuration = TimeSpan.FromSeconds(12);
                    bwp[i].ReadFully = false;
                }


                int opus_frame_length = 48000 * 12;


                int channel_count;

                channel_count = 4;
                NAudio.Wave.WaveFormat opus_format4 = NAudio.Wave.WaveFormat.CreateCustomFormat(NAudio.Wave.WaveFormatEncoding.Pcm, 48000, channel_count, 48000 * channel_count * 2, channel_count * 2, 16);

                channel_count = 1;
                NAudio.Wave.WaveFormat opus_format1 = NAudio.Wave.WaveFormat.CreateCustomFormat(NAudio.Wave.WaveFormatEncoding.Pcm, 48000, channel_count, 48000 * channel_count * 2, channel_count * 2, 16);



                byte[] buffer = new byte[capture.WaveFormat.BitsPerSample * capture.WaveFormat.Channels / 8 + 1000];

                short[] pcm_buffer = new short[opus_frame_length];


                object wave_lock = new object();

                BlockingCollection<(byte[], int)> queue = new BlockingCollection<(byte[], int)>(new ConcurrentQueue<(byte[], int)>());

                capture.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs e) =>
                {
                    byte[] buffer2 = new byte[e.BytesRecorded];

                    Buffer.BlockCopy(e.Buffer, 0, buffer2, 0, e.BytesRecorded);

                    queue.Add((buffer2, e.BytesRecorded));
                };

                Thread thread = new Thread(() =>
                {
                    while (true)
                    {
                        (byte[] buffer3, int bytesRecorded) = queue.Take();

                        lock (wave_lock)
                        {
                            for (int i = 0; i < bwp.Length; i++)
                            {
                                bwp[i].AddSamples(buffer3, 0, bytesRecorded);


                                if (bwp[i].BufferedBytes >= bwp[i].WaveFormat.AverageBytesPerSecond * 10)
                                {
                                    NAudio.Wave.ResamplerDmoStream rds = new NAudio.Wave.ResamplerDmoStream(bwp[i], opus_format4);


                                    NAudio.Wave.MultiplexingWaveProvider mwp2 = new NAudio.Wave.MultiplexingWaveProvider(new NAudio.Wave.IWaveProvider[] { rds, }, 1);
                                    mwp2.ConnectInputToOutput(i, 0);


                                    while (true)
                                    {

                                        int count = mwp2.Read(buffer, 0, buffer.Length);

                                        if (count == 0)
                                        {
                                            break;
                                        }



                                        short[] sdata = new short[count / 2];
                                        Buffer.BlockCopy(buffer, 0, sdata, 0, count);

                                        short max = 0;
                                        for (int si = 0; si < sdata.Length; si++)
                                        {
                                            max = Math.Max(max, Math.Abs(sdata[si]));
                                        }
                                        //Console.WriteLine($"channel {i}: count is {count}, max is {max}");


                                        if (max < 500)
                                        {
                                            oggfiles[i].WriteSamples(silence, 0, count / 2);
                                        }
                                        else
                                        {
                                            oggfiles[i].WriteSamples(sdata, 0, count / 2);
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
                thread.IsBackground = true;
                thread.Name = "Encoding thread";
                thread.Start();



                // attempt to align audio recording with seconds
                int five_second = DateTimeOffset.Now.Second / 5;
                while (DateTimeOffset.Now.Second / 5 == five_second)
                {
                    // wait
                }


                capture.StartRecording();
                DateTimeOffset start = DateTimeOffset.Now;
                DateTimeOffset last_report = DateTimeOffset.MinValue;
                TimeSpan span = TimeSpan.FromMinutes(1);

                while (true)
                {
                    DateTimeOffset now = DateTimeOffset.Now;

                    if (last_report + span < now)
                    {
                        Console.WriteLine($"{(now - start).TotalMinutes} minutes elapsed");
                        last_report = now;
                    }


                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }


                capture.StopRecording();
                Console.WriteLine($"recorded for {DateTimeOffset.Now - start}");



                lock (wave_lock)
                {
                    for (int i = 0; i < oggfiles.Length; i++)
                    {
                        oggfiles[i].Finish();
                    }
                }
            }
        }

        public static Concentus.Oggfile.OpusOggWriteStream CreateOgg(string filename)
        {
            Concentus.Structs.OpusEncoder encoder = Concentus.Structs.OpusEncoder.Create(48000, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.UseVBR = true;
            encoder.Bitrate = 1024 * 10;


            System.IO.File.Delete(filename);

            System.IO.FileStream os = new System.IO.FileStream(filename, System.IO.FileMode.OpenOrCreate);
            return new Concentus.Oggfile.OpusOggWriteStream(encoder, os);
        }


        public static DateTimeOffset RoundDownToFiveMinutes(DateTimeOffset now)
        {
            long ms = now.Ticks - now.Ticks % TimeSpan.FromMinutes(5).Ticks;

            return new DateTimeOffset(ms, now.Offset);
        }
    }
}
