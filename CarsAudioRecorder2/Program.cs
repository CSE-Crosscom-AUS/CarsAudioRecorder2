﻿using System;
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

        public static string CompanyName => "CSECrosscom";
        public static string ApplicationName => "CarsAudioRecorder2";

        static string LogFileDir = "";
        static string LogFileName = "";
        static System.IO.StreamWriter LogFile;

        static string RecordingFolder => MakeSettingsFolder("Recordings");



        private static string last_log_folder = "";


        static void Main(string[] args)
        {
            DateTimeOffset CurrentBlockTs = RoundDownToFiveSeconds(DateTimeOffset.Now + TimeSpan.FromSeconds(5));


            NAudio.CoreAudioApi.MMDevice InputDevice = GetMMDevice();


            if (InputDevice != null)
            {
                Record(InputDevice, CurrentBlockTs);
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


        public static void Record(NAudio.CoreAudioApi.MMDevice InputDevice, DateTimeOffset StartTimeTs)
        {

            short[] silence = new short[2000];

            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice, false, 1000))
            {

                Block[] CurrentBlocks = new Block[4];



                for (int i = 0; i < CurrentBlocks.Length; i++)
                {


                    CurrentBlocks[i] = new Block(StartTimeTs, i);

                    LogWriteLine($"new Block {CurrentBlocks[i].Channel} {CurrentBlocks[i].BlockStartTs} {CurrentBlocks[i].FinalSampleCount}");
                }


                while (StartTimeTs < RoundDownToFiveSeconds(DateTimeOffset.Now))
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


                                        int sample_count = count / 2;




                                        int block_remaining_samples = CurrentBlocks[i].FinalSampleCount - CurrentBlocks[i].CurrentSampleCount;

                                        int step_one_sample_count;

                                        if (block_remaining_samples >= sample_count)
                                        {
                                            step_one_sample_count = sample_count;
                                        }
                                        else
                                        {
                                            step_one_sample_count = block_remaining_samples;
                                        }

                                        int step_two_sample_count = sample_count - step_one_sample_count;



                                        if (max < 500)
                                        {
                                            CurrentBlocks[i].OggFile.WriteSamples(silence, 0, step_one_sample_count);
                                        }
                                        else
                                        {
                                            CurrentBlocks[i].OggFile.WriteSamples(sdata, 0, step_one_sample_count);
                                        }
                                        CurrentBlocks[i].CurrentSampleCount += step_one_sample_count;


                                        if (CurrentBlocks[i].CurrentSampleCount >= CurrentBlocks[i].FinalSampleCount)
                                        {
                                            if (CurrentBlocks[i].CurrentSampleCount > CurrentBlocks[i].FinalSampleCount)
                                            {
                                                LogFile.WriteLine("This should never happen");
                                            }

                                            LogWriteLine($"finishing Block {i} {CurrentBlocks[i].CurrentSampleCount} {CurrentBlocks[i].FinalSampleCount}");

                                            CurrentBlocks[i].OggFile.Finish();
                                            Block oldblock = CurrentBlocks[i];

                                            DateTimeOffset new_block_start_ts = RoundDownToFiveMinutes(oldblock.BlockStartTs + TimeSpan.FromMinutes(5));

                                            CurrentBlocks[i] = new Block(new_block_start_ts, i);


                                            LogWriteLine($"new Block {i} {CurrentBlocks[i].BlockStartTs} {CurrentBlocks[i].FinalSampleCount}");



                                            if (max < 500)
                                            {
                                                CurrentBlocks[i].OggFile.WriteSamples(silence, step_one_sample_count, step_two_sample_count);
                                            }
                                            else
                                            {
                                                CurrentBlocks[i].OggFile.WriteSamples(sdata, step_one_sample_count, step_two_sample_count);
                                            }
                                            CurrentBlocks[i].CurrentSampleCount += step_two_sample_count;

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


                LogWriteLine("end!");
            }
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


        public static string GetFileName(DateTimeOffset ts, int channel)
        {
            return $"recording-{ts.Year:0000}{ts.Month:00}{ts.Day:00}-{ts.Hour:00}{ts.Minute:00}{ts.Second:00}-ch{channel:00}.opus";
        }


        private static void CheckLogFile()
        {
            string new_log_file_dir = CreateDateFolder(DateTimeOffset.Now);

            if (new_log_file_dir != LogFileDir)
            {
                LogFile?.Close();
                LogFile = null;
            }

            if (LogFile == null)
            {
                DateTimeOffset ts = DateTimeOffset.Now;
                LogFileDir = new_log_file_dir;
                LogFileName = System.IO.Path.Combine(LogFileDir, $"recording-{ts.Year:0000}{ts.Month:00}{ts.Day:00}-{ts.Hour}{ts.Minute}{ts.Second}.txt");
                LogFile = new System.IO.StreamWriter(LogFileName);
            }
        }

        public static void LogWrite(string s)
        {
            CheckLogFile();

            Console.Write(s);

            LogFile.Write(s);
        }

        public static void LogWriteLine(string s = "")
        {
            CheckLogFile();

            Console.WriteLine(s);

            LogFile.WriteLine(s);
            LogFile.Flush();
        }


        /// <summary>
        /// Make a settings folder in ~/AppData/Local or ~/AppData/Roaming
        /// </summary>
        /// <param name="name">The name of the subfolder to create. Can be "" for the base folder.</param>
        /// <param name="roaming">true: use "Local", false: use "Roaming"</param>
        /// <returns>The full path of the folder</returns>
        public static string MakeSettingsFolder(string name, string application_name = null, Environment.SpecialFolder folderType = Environment.SpecialFolder.LocalApplicationData)
        {

            string path = Environment.GetFolderPath(folderType);

            if (!System.IO.Directory.Exists(path))
            {
                // bad error
                return null;
            }

            if (application_name == null)
            {
                application_name = ApplicationName;
            }

            foreach (string path_bit in new List<string> { CompanyName, application_name, name })
            {
                if (path_bit == "")
                {
                    continue;
                }

                path = System.IO.Path.Combine(path, path_bit);

                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.DirectoryInfo di = System.IO.Directory.CreateDirectory(path);

                    if (!di.Exists)
                    {
                        // error
                        return null;
                    }
                }
            }

            return path;
        }


        public static string CreateDateFolder(DateTimeOffset ts)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);


            foreach (string path_bit in new List<string> { "", Program.CompanyName, Program.ApplicationName, "Recordings", ts.Year.ToString("0000"), ts.Month.ToString("00"), ts.Day.ToString("00"), })
            {

                if (path_bit != "")
                {
                    path = System.IO.Path.Combine(path, path_bit);
                }

                if (!System.IO.Directory.Exists(path))
                {
                    System.IO.DirectoryInfo di = System.IO.Directory.CreateDirectory(path);

                    if (!di.Exists)
                    {
                        // error
                        throw new Exception($"Can't create/find folder {path}");
                    }
                }
            }

            return path;
        }
    }





    public class Block
    {
        public DateTimeOffset BlockStartTs { get; set; }
        public Concentus.Oggfile.OpusOggWriteStream OggFile { get; set; }
        public string FileName { get; set; }
        public int CurrentSampleCount { get; set; } = 0;
        public int FinalSampleCount { get; set; }

        public int Channel { get; set; }

        public Block(DateTimeOffset startTs, int channel)
        {

            string path = Program.CreateDateFolder(startTs);

            BlockStartTs = startTs;
            Channel = channel;
            FileName = System.IO.Path.Combine(path, Program.GetFileName(startTs, channel));



            Concentus.Structs.OpusEncoder encoder = Concentus.Structs.OpusEncoder.Create(48000, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_AUDIO);
            encoder.UseVBR = true;
            encoder.Bitrate = 1024 * 10;


            DateTimeOffset NextBlockTs = Program.RoundDownToFiveMinutes(BlockStartTs + TimeSpan.FromMinutes(5) + TimeSpan.FromMinutes(1));

            TimeSpan StartBlockSpan = NextBlockTs - BlockStartTs;
            FinalSampleCount = (int)(StartBlockSpan.TotalSeconds * 48000);


            System.IO.FileStream os = new System.IO.FileStream(FileName, System.IO.FileMode.OpenOrCreate);
            OggFile = new Concentus.Oggfile.OpusOggWriteStream(encoder, os);

            Program.LogWriteLine($"new block {channel} {FileName}");
        }
    }
}
