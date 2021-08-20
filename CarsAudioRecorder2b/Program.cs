using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarsAudioRecorder2b
{
    class Program
    {
        static void Main(string[] args)
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


            using (NAudio.CoreAudioApi.WasapiCapture capture = new NAudio.CoreAudioApi.WasapiCapture(InputDevice))
            {
                NAudio.Wave.WaveFileWriter wave = new NAudio.Wave.WaveFileWriter("out.wav", capture.WaveFormat);

                ////NAudio.Wave.BufferedWaveProvider bufferedWaveProvider = new NAudio.Wave.BufferedWaveProvider(capture.WaveFormat);


                //NAudio.Wave.WaveInProvider wip = new NAudio.Wave.WaveInProvider(capture);




                //NAudio.Wave.IWaveProvider[] wp = new NAudio.Wave.IWaveProvider[] { wip, };

                //NAudio.Wave.MultiplexingWaveProvider mwp1 = new NAudio.Wave.MultiplexingWaveProvider(wp, 1);

                //Console.WriteLine($"mwp1.InputChannelCount is {mwp1.InputChannelCount}");
                //Console.WriteLine($"mwp1.OutputChannelCount is {mwp1.OutputChannelCount}");

                //mwp1.ConnectInputToOutput(0, 0);



                //NAudio.Wave.WaveFileWriter.CreateWaveFile("out1.wav", mwp1);


                //NAudio.Wave.MultiplexingWaveProvider mwp2 = new NAudio.Wave.MultiplexingWaveProvider(wp, 1);
                //mwp1.ConnectInputToOutput(1, 0);
                //NAudio.Wave.WaveFileWriter wav2 = new NAudio.Wave.WaveFileWriter("out2.wav", mwp2.WaveFormat);


                //NAudio.Wave.MultiplexingWaveProvider mwp3 = new NAudio.Wave.MultiplexingWaveProvider(wp, 1);
                //mwp1.ConnectInputToOutput(2, 0);
                //NAudio.Wave.WaveFileWriter wav3 = new NAudio.Wave.WaveFileWriter("out3.wav", mwp3.WaveFormat);


                //NAudio.Wave.MultiplexingWaveProvider mwp4 = new NAudio.Wave.MultiplexingWaveProvider(wp, 1);
                //mwp1.ConnectInputToOutput(3, 0);
                //NAudio.Wave.WaveFileWriter wav4 = new NAudio.Wave.WaveFileWriter("out4.wav", mwp4.WaveFormat);






                capture.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs e) =>
                {
                    wave.Write(e.Buffer, 0, e.BytesRecorded);
                };
                //    bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
                //    Console.WriteLine($"e.BytesRecorded is {e.BytesRecorded}");


                //    byte[] buffer = new byte[1024 * 50];
                //    int count = 0;

                //    do
                //    {
                //        count = mwp1.Read(buffer, 0, buffer.Length);
                //        Console.WriteLine($"count is {count}");
                //        wav1.Write(buffer, 0, count);
                //    }
                //    while (!bufferedWaveProvider.ReadFully);



                //    //do
                //    //{
                //    //    count = mwp2.Read(buffer, 0, buffer.Length);
                //    //    wav2.Write(buffer, 0, count);
                //    //}
                //    //while (count > 0);


                //    //do
                //    //{
                //    //    count = mwp3.Read(buffer, 0, buffer.Length);
                //    //    wav3.Write(buffer, 0, count);
                //    //}
                //    //while (count > 0);


                //    //do
                //    //{
                //    //    count = mwp4.Read(buffer, 0, buffer.Length);
                //    //    wav4.Write(buffer, 0, count);
                //    //}
                //    //while (count > 0);
                //};






                capture.StartRecording();

                Console.ReadKey();

                capture.StopRecording();

                wave.Dispose();

                //wav1.Dispose();
                //wav2.Dispose();
                //wav3.Dispose();
                //wav4.Dispose();
            }


            Console.WriteLine("done");
        }
    }
}
