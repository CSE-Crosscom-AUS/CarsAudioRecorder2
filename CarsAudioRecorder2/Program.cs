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
            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice))
            {




                Concentus.Oggfile.OpusOggWriteStream[] ogg = new Concentus.Oggfile.OpusOggWriteStream[4];
                Concentus.Structs.OpusEncoder[] encoders = new Concentus.Structs.OpusEncoder[4]; //OpusEncoder.Create(48000, 1, OpusApplication.OPUS_APPLICATION_AUDIO);
                for (int i = 0; i < encoders.Length; i++)
                {
                    encoders[i] = Concentus.Structs.OpusEncoder.Create(48000, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP);
                    encoders[i].UseVBR = true;
                    encoders[i].Bitrate = 6144 * 5;


                    System.IO.FileStream os = new System.IO.FileStream($"out{i}.opus", System.IO.FileMode.OpenOrCreate);
                    ogg[i] = new Concentus.Oggfile.OpusOggWriteStream(encoders[i], os);

                }


                //encoder.Bitrate = 12000;

                //OpusDotNet.OpusEncoder[] oe = new OpusDotNet.OpusEncoder[4];
                //for (int i = 0; i < oe.Length; i++)
                //{
                //    oe[i] = new OpusDotNet.OpusEncoder(OpusDotNet.Application.VoIP, 48000, 1)
                //    {
                //        VBR = true,
                //    };
                //}

                Queue<byte>[] queue = new Queue<byte>[4];

                for (int i = 0; i < queue.Length; i++)
                {
                    queue[i] = new Queue<byte>();
                }


                NAudio.Wave.BufferedWaveProvider[] bwp = new NAudio.Wave.BufferedWaveProvider[4];

                for (int i = 0; i < bwp.Length; i++)
                {
                    bwp[i] = new NAudio.Wave.BufferedWaveProvider(capture.WaveFormat);
                    bwp[i].ReadFully = false;
                }


                int opus_frame_length = 48000 * 2 / 50;


                int channel_count;

                channel_count = 4;
                NAudio.Wave.WaveFormat opus_format4 = NAudio.Wave.WaveFormat.CreateCustomFormat(NAudio.Wave.WaveFormatEncoding.Pcm, 48000, channel_count, 48000 * channel_count * 2, channel_count * 2, 16);

                channel_count = 1;
                NAudio.Wave.WaveFormat opus_format1 = NAudio.Wave.WaveFormat.CreateCustomFormat(NAudio.Wave.WaveFormatEncoding.Pcm, 48000, channel_count, 48000 * channel_count * 2, channel_count * 2, 16);



                byte[] buffer = new byte[capture.WaveFormat.BitsPerSample * capture.WaveFormat.Channels / 8 + 1000];

                short[] pcm_buffer = new short[opus_frame_length];
                //byte[] opus_buffer = new byte[opus_frame_length];


                object wave_lock = new object();

                capture.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs e) =>
                {

                    lock (wave_lock)
                    {
                        for (int i = 0; i < bwp.Length; i++)
                        {
                            bwp[i].AddSamples(e.Buffer, 0, e.BytesRecorded);

                            if (bwp[i].BufferLength >= 48000 * capture.WaveFormat.BitsPerSample * capture.WaveFormat.Channels / 8)
                            {
                                Console.WriteLine($"buffer duration is {bwp[i].BufferDuration.TotalMilliseconds}");

                                Console.WriteLine("hi");


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

                                    //ms[i].Write(buffer, 0, count);
                                    for (int j = 0; j < count; j++)
                                    {
                                        queue[i].Enqueue(buffer[j]);
                                    }
                                }

                            }



                            bwp[i].ClearBuffer();
                        }



                        for (int i = 0; i < queue.Length; i++)
                        {
                            while (queue[i].Count > 0)
                            {
                                for (int j = 0; j < opus_frame_length; j++)
                                {
                                    if (queue[i].Count == 0)
                                    {
                                        break;
                                    }
                                    var low = queue[i].Dequeue();
                                    var high = queue[i].Dequeue() * 256;

                                    pcm_buffer[j] = (short)(high + low);
                                }

                                ogg[i].WriteSamples(pcm_buffer, 0, opus_frame_length);
                            }
                        }
                    }
                };




                //NAudio.Wave.MultiplexingWaveProvider[] mwp = new NAudio.Wave.MultiplexingWaveProvider[4];

                //for (int i = 0; i < mwp.Length; i++)
                //{
                //    mwp[i] = new NAudio.Wave.MultiplexingWaveProvider(new NAudio.Wave.IWaveProvider[] { bwp[i], }, 1);
                //    mwp[i].ConnectInputToOutput(i, 0);
                //}





                //capture.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs e) =>
                //{
                //    for (int i = 0; i < bwp.Length; i++)
                //    {
                //        bwp[i].AddSamples(e.Buffer, 0, e.BytesRecorded);
                //    }
                //};



                //NAudio.Wave.IWavePlayer[] wo = new DiskWavePlayer[4];

                //for (int i = 0; i < wo.Length; i++)
                //{
                //    wo[i] = new DiskWavePlayer($"out{i}.wav");

                //    wo[i].Init(mwp[i]);
                //    wo[i].Play();
                //}



                capture.StartRecording();

                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                    Console.WriteLine("yeah");
                }


                capture.StopRecording();


                //for (int i = 0; i < wo.Length; i++)
                //{
                //    wo[i].Stop();
                //    wo[i].Dispose();
                //}

                lock (wave_lock)
                {
                    //for (int i = 0; i < wave.Length; i++)
                    //{
                    //    wave[i].Dispose();
                    //}

                    for (int i = 0; i < ogg.Length; i++)
                    {
                        ogg[i].Finish();
                    }
                }
            }
        }
    }
}
