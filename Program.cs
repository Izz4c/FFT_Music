using NAudio.Wave;
using System.Numerics;

class AudioRecorder
{
    private WaveInEvent waveIn;
    private List<float> samples = new List<float>();
    public int samplecount = 0;
    public void StartRecording(int sampleRate = 44100)
    {
        samples.Clear();
        waveIn = new WaveInEvent();
        waveIn.WaveFormat = new WaveFormat(sampleRate, 1);

        waveIn.DataAvailable += OnDataAvailable;
        waveIn.StartRecording();
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short raw = BitConverter.ToInt16(e.Buffer, i);
            samples.Add(raw / 32768f);
            samplecount++;
        }
    }

    public float[] StopRecording()
    {
        waveIn.StopRecording();
        waveIn.Dispose();
        return samples.ToArray();
    }
    public void Clean()
    {
        samples.Clear();
        samplecount = 0;
    }
}



class Program
{
    // The FFT Algorithm, improved version of DFT that can be 1000s of times faster
    static Complex[] FFT(Complex[] samples)
    {
        int N = samples.Length;
        if (N == 1)                        // If only one sample, just return it as it is. (DFT N=1)
        {
            return samples;
        }
        Complex[] even = new Complex[N/2]; // Split the samples into even and odd indices
        Complex[] odd  = new Complex[N/2];
        for (int i = 0; i < N / 2; i++)
        {
            even[i] = samples[2 * i];
            odd[i]  = samples[2 * i + 1];
        }
        even = FFT(even);                  // Perform FFT on even index samples.
        odd = FFT(odd);                    // Perform FFT on odd  index samples
        Complex[] result = new Complex[N]; // Output array
        for(int k = 0; k < N / 2; k++) 
        {
            Complex t = Complex.FromPolarCoordinates(1, -6.283185307179586 * k / N) * odd[k];
            result[k]         = even[k] + t;    // bottom half of frequency domain
            result[k + N / 2] = even[k] - t;    // top half of frequency domain
                                                //(this is the symmetry we exploit that gets us nlogn complexity)
        }
        return result;

    }
    // Convert Note to MIDI Number
    static int NoteToMIDI(string note)
    {
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }; // Note names
        note = note.Replace("Bb", "A#").Replace("Eb", "D#")     // Bb is same as A#, Eb is same as D#, and so on
               .Replace("Ab", "G#").Replace("Db", "C#")
               .Replace("Gb", "F#").Replace(" ","");
        string notename = note.Substring(0,note.Length-1);      // Get the note name by removing the last character from input
        int octave = int.Parse(note[note.Length-1].ToString()); // Get octave number from the last character of the input
        int semitone = Array.IndexOf(notes,notename);           // Get semitone number by finding the index of the note in the notes array
        return (octave+1)*12 + semitone;                        // MIDI number formula
    }
    // Convert MIDI number to a Note
    static string MIDIToNote(int midi)
    {
        midi = Math.Clamp(midi,0,999);
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }; // Note names
    
        int octave   = (midi / 12) - 1;     // You'll get this by rearranging (octave+1)*12 + semitone = midi and knowing that semitone < 12
        int semitone = midi % 12;           // Because semitone = midi - ((midi//12)-1+1)*12 = midi - (12*midi//12) = midi%12 
    
        return notes[semitone] + octave;    // Concatenate note and octave number 
    }
    // Convert a float array to a complex number array for FFT algorithm
    static Complex[] ToComplex(float[] x)
    {
        Complex[] result = new Complex[x.Length];
        for (int i = 0; i < x.Length; i++)
            result[i] = new Complex(x[i], 0);
        return result;
    }
    static void Main()
    {
        // Create the recorder object
        AudioRecorder recorder = new AudioRecorder();

        Console.WriteLine("1. Generate Notes\n2. Generate Scale\n3. Harmonize\n4. FFT Analysis\n");
        Console.Write("Enter Choice : ");
        int choice = int.Parse(Console.ReadLine());

        if(choice == 1)
        {
            Console.Write("Enter sample rate (default 44100Hz) : ");
            string input = Console.ReadLine();
            int sampleRate = 44100;
            if(input!="") sampleRate = int.Parse(input);
            Console.Write("Enter first note (None for A4-440Hz): ");
            string firstnote = Console.ReadLine();
            int midi;
            double firstFreq;
            if (firstnote == "")
            {
                midi = 69;       // A4
                firstFreq = 440; // 440 Hz
            }
            else
            {
                midi = NoteToMIDI(firstnote);
                firstFreq = 0;  // To be captured
            }
            
            Console.WriteLine("Enter Q to Stop.");
            while (true)
            {
                if(Console.ReadLine().ToLower()=="q") break;

                // Recording
                recorder.StartRecording(sampleRate);
                while(recorder.samplecount < sampleRate/2) Thread.Sleep(10);;
                float[] samples = recorder.StopRecording();

                // Fast Fourier Transform
                Complex[] result = FFT(ToComplex(samples));
                
                // Find the peak frequency
                int peakK = 0;
                float peakMag = 0;
                for (int k = 0; k < samples.Length/2; k++)
                {
                    float mag = (float)result[k].Magnitude;
                    if (mag > peakMag)
                    {
                        peakMag = mag;
                        peakK = k;
                    }
                }
                double peak = peakK * sampleRate / samples.Length;
                
                if(firstFreq==0) {
                    Console.Write("\x1b[38;2;255;165;0m"+MIDIToNote(midi)+"\x1b[0m <--> "+(int)peak+" ±"+sampleRate/samples.Length+"Hz");
                    firstFreq = peak;
                }
                else
                {
                    int num = (int)Math.Round(midi+12*Math.Log2(peak/firstFreq));
                    Console.Write("\x1b[38;2;255;165;0m"+MIDIToNote(num)+"\x1b[0m <--> "+(int)peak+" ±"+sampleRate/samples.Length+"Hz");
                }
                // Play back the tone
                Console.Beep(Math.Clamp((int)peak,40,10000),500);
                // Clean Up
                Array.Clear(samples,0,samples.Length);
                recorder.Clean();
            }
        }
        if(choice == 2)
        {
            Console.Write("1. Major\n2. Minor\n3. Augmented\n4. Diminished\n5. Mixolydian\nEnter choice : ");
            int scale = int.Parse(Console.ReadLine());
            
            Console.Write("Enter root note : ");
            int note = NoteToMIDI(Console.ReadLine());

            int[] intervals = {0,2,2,1,2,2,2,1,0,2,1,2,2,1,2,2,0,3,1,3,1,3,1,0,2,1,2,1,2,1,2,0,2,2,1,2,2,1,2,0};
            for(int i = 0; i < 100; i++)
            {
                int increment = intervals[i+(scale-1)*8];
                if(i!=0&&increment==0){
                    break;
                }   
                note+=increment;
                Console.WriteLine(MIDIToNote(note));
            }
        }
        if(choice == 3)
        {
            Console.Write("Enter shift : ");
            int shift = int.Parse(Console.ReadLine());
            Console.WriteLine("Enter notes separated by commas : ");
            string notes = Console.ReadLine();
            string[] notearray = notes.Replace(" ","").Split(',');
            foreach(string note in notearray)
            {
                Console.Beep((int)(440*Math.Pow(2,(NoteToMIDI(note)+shift-69)/12.0f)),300);
                Console.Write(MIDIToNote(NoteToMIDI(note)+shift)+", ");
            }
        }
        if(choice == 4)
        {
            Console.Write("Enter sample rate (default 44100Hz) : ");
            string input = Console.ReadLine();
            int sampleRate = 44100;
            if(input!="") sampleRate = int.Parse(input);
            Console.Write("Enter number of top frequencies to view : ");
            input = Console.ReadLine();
            int numPeaks = int.Parse(input);
            recorder.StartRecording(sampleRate);
            while(recorder.samplecount < sampleRate/2) Thread.Sleep(10);
            float[] samples = recorder.StopRecording();

            // Fast Fourier Transform
            Complex[] result = FFT(ToComplex(samples));
            Console.WriteLine("FFT Complete!");
            int[] sortedBins = Enumerable.Range(0, result.Length / 2)
            .OrderByDescending(k => result[k].Magnitude)
            .ToArray();
            List<float> used = new List<float>();
            int i=0,j=0;
            while(true)
            {
                if (j >= numPeaks)
                {
                    break;
                }
                bool overlap = false;
                for(int k = -10; k <= 10; k++)
                {
                    if (used.Contains(sortedBins[i]+k))
                    {
                        overlap = true;
                        break;
                    }
                }
                
                if(overlap) {
                    i++;
                    continue;
                }
                used.Add(sortedBins[i]);
                Console.WriteLine(j+") \x1b[38;2;255;165;0m"+sortedBins[i]*sampleRate/samples.Length+"\x1b[0m Hz");
                j++;i++;
            }
            // Clean Up
            Array.Clear(samples,0,samples.Length);
            recorder.Clean();
        }
        
    }
}
