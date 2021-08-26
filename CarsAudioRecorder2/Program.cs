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

        static System.IO.StreamWriter LogFile;

        static DateTimeOffset CurrentBlockTs;

        static void Main(string[] args)
        {
            CurrentBlockTs = RoundDownToFiveSeconds(DateTimeOffset.Now + TimeSpan.FromSeconds(5));
            LogFile = new System.IO.StreamWriter(System.IO.Path.Combine("recording", $"recording-{CurrentBlockTs.Year:0000}{CurrentBlockTs.Month:00}{CurrentBlockTs.Day:00}-{CurrentBlockTs.Hour:00}{CurrentBlockTs.Minute:00}{CurrentBlockTs.Second:00}.txt"));




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
                    LogWrite($"{device.FriendlyName}, {device.State}");

                    if (InputDevice == null)
                    {
                        if (device.FriendlyName.Contains("BEHRINGER UMC 404HD") && device.FriendlyName.Contains("1-4"))
                        {
                            LogWrite("!!!");
                            InputDevice = device;
                        }
                    }

                    LogWriteLine();
                }
            }

            return InputDevice;
        }


        public static void Record(NAudio.CoreAudioApi.MMDevice InputDevice)
        {

            short[] silence = new short[2000];

            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice, false, 1000))
            {
                //Concentus.Oggfile.OpusOggWriteStream[] oggfiles = new Concentus.Oggfile.OpusOggWriteStream[4];

                //for (int i = 0; i < oggfiles.Length; i++)
                //{
                //    oggfiles[i] = CreateOgg($"out{i}.opus");
                //}

                string recordingFolder = "recording";

                System.IO.Directory.CreateDirectory(recordingFolder);

                Block[] CurrentBlocks = new Block[4];


                while (CurrentBlockTs < RoundDownToFiveSeconds(DateTimeOffset.Now))
                {
                    // wait
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


                                    DateTimeOffset PosibleNextBlockTs = RoundDownToFiveMinutes(DateTimeOffset.Now);
                                    if (PosibleNextBlockTs > CurrentBlockTs)
                                    {
                                        CurrentBlockTs = PosibleNextBlockTs;
                                    }

                                    while (true)
                                    {

                                        int count = mwp2.Read(buffer, 0, buffer.Length);

                                        if (count == 0)
                                        {
                                            break;
                                        }

                                        if (CurrentBlocks[i] == null || CurrentBlocks[i].BlockTs < CurrentBlockTs)
                                        {
                                            CurrentBlocks[i]?.OggFile?.Finish();
                                            LogWriteLine($"Channel {i}: RawSampleCount {CurrentBlocks[i]?.RawSampleCount}");

                                            string fn = System.IO.Path.Combine(recordingFolder, $"recording-{CurrentBlockTs.Year:0000}{CurrentBlockTs.Month:00}{CurrentBlockTs.Day:00}-{CurrentBlockTs.Hour:00}{CurrentBlockTs.Minute:00}{CurrentBlockTs.Second:00}-ch{i:00}.opus");

                                            CurrentBlocks[i] = new Block
                                            {
                                                BlockTs = CurrentBlockTs,
                                                OggFile = CreateOgg(fn),
                                                FileName = fn,
                                            };
                                        }


                                        short[] sdata = new short[count / 2];
                                        Buffer.BlockCopy(buffer, 0, sdata, 0, count);

                                        short max = 0;
                                        for (int si = 0; si < sdata.Length; si++)
                                        {
                                            max = Math.Max(max, Math.Abs(sdata[si]));
                                        }


                                        // need to move the block creation to here (so we can make blocks exactly 5 minutes

                                        if (max < 500)
                                        {
                                            CurrentBlocks[i].OggFile.WriteSamples(silence, 0, count / 2);
                                        }
                                        else
                                        {
                                            CurrentBlocks[i].OggFile.WriteSamples(sdata, 0, count / 2);
                                        }
                                        CurrentBlocks[i].RawSampleCount += count / 2;
                                    }
                                }
                            }
                        }
                    }
                });
                thread.IsBackground = true;
                thread.Name = "Encoding thread";
                thread.Start();


                capture.StartRecording();
                DateTimeOffset start = DateTimeOffset.Now;
                DateTimeOffset last_report = DateTimeOffset.MinValue;
                TimeSpan span = TimeSpan.FromMinutes(1);

                while (true)
                {
                    DateTimeOffset now = DateTimeOffset.Now;

                    if (last_report + span < now)
                    {
                        LogWriteLine($"{(now - start).TotalMinutes} minutes elapsed");
                        last_report = now;
                    }


                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }


                capture.StopRecording();
                LogWriteLine($"recorded for {DateTimeOffset.Now - start}");


                lock (wave_lock)
                {
                    for (int i = 0; i < CurrentBlocks.Length; i++)
                    {
                        CurrentBlocks[i]?.OggFile?.Finish();
                    }
                }


                LogFile.WriteLine("end!");
                LogFile.Close();
            }
        }

        public static Concentus.Oggfile.OpusOggWriteStream CreateOgg(string filename)
        {
            Concentus.Structs.OpusEncoder encoder = Concentus.Structs.OpusEncoder.Create(48000, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.UseVBR = true;
            encoder.Bitrate = 1024 * 10;


            //System.IO.File.Delete(filename);

            System.IO.FileStream os = new System.IO.FileStream(filename, System.IO.FileMode.OpenOrCreate);
            return new Concentus.Oggfile.OpusOggWriteStream(encoder, os);
        }



        public static DateTimeOffset RoundDownToFiveSeconds(DateTimeOffset now)
        {
            long ms = now.Ticks - now.Ticks % TimeSpan.FromSeconds(5).Ticks;

            return new DateTimeOffset(ms, now.Offset);
        }


        public static DateTimeOffset RoundDownToFiveMinutes(DateTimeOffset now)
        {
            long ms = now.Ticks - now.Ticks % TimeSpan.FromMinutes(5).Ticks;

            return new DateTimeOffset(ms, now.Offset);
        }


        public static void LogWrite(string s)
        {
            Console.Write(s);

            LogFile.Write(s);
        }

        public static void LogWriteLine(string s = "")
        {
            Console.WriteLine(s);

            LogFile.WriteLine(s);
            LogFile.Flush();
        }
    }

    public class Block
    {
        public DateTimeOffset BlockTs { get; set; }
        public Concentus.Oggfile.OpusOggWriteStream OggFile { get; set; }
        public string FileName { get; set; }
        public int RawSampleCount { get; set; } = 0;
    }
}
