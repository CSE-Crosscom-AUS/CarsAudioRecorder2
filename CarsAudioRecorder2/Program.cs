using System;
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

            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice))
            {
                Concentus.Oggfile.OpusOggWriteStream[] ogg = new Concentus.Oggfile.OpusOggWriteStream[4];
                Concentus.Structs.OpusEncoder[] encoders = new Concentus.Structs.OpusEncoder[4];
                for (int i = 0; i < encoders.Length; i++)
                {
                    encoders[i] = Concentus.Structs.OpusEncoder.Create(48000, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
                    encoders[i].UseVBR = true;
                    encoders[i].Bitrate = 1024 * 10;


                    string fn = $"out{i}.opus";

                    System.IO.File.Delete(fn);

                    System.IO.FileStream os = new System.IO.FileStream(fn, System.IO.FileMode.OpenOrCreate);
                    ogg[i] = new Concentus.Oggfile.OpusOggWriteStream(encoders[i], os);

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

                capture.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs e) =>
                {

                    lock (wave_lock)
                    {
                        for (int i = 0; i < bwp.Length; i++)
                        {
                            bwp[i].AddSamples(e.Buffer, 0, e.BytesRecorded);


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
                                        ogg[i].WriteSamples(silence, 0, count / 2);
                                    }
                                    else
                                    {
                                        ogg[i].WriteSamples(sdata, 0, count / 2);
                                    }
                                }
                            }
                        }
                    }
                };



                capture.StartRecording();

                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }


                capture.StopRecording();



                lock (wave_lock)
                {
                    for (int i = 0; i < ogg.Length; i++)
                    {
                        ogg[i].Finish();
                    }
                }
            }
        }
    }
}
