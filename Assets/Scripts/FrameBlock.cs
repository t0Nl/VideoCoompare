// A simple class to track and store data realting to an encoded chunk of frames.
public class FrameBlock
{
    public int startFrameIndex;
    public int endFrameIndex;
    public int frameBlockSize;
    public int numberOfIFrames;
    public double framesEncodedPerSecond;
    public double avgPsnr;
    public double avgSsim;
    public double avgBrisque;
    public double avgNiqe;

    private double totalBrisque;
    private double totalNiqe;
    private bool[] finalPassTracker;
    private bool finalPassComplete;

    // Constructor
    public FrameBlock(int startIndex, int endIndex)
    {
        startFrameIndex = startIndex;
        endFrameIndex = endIndex;
        frameBlockSize = endIndex - startIndex + 1;
        finalPassTracker = new bool[frameBlockSize];
        finalPassComplete = false;
        totalBrisque = 0;
        totalNiqe = 0;
    }

    // Returns the relative index of the next frame for NIQE and BRISQUE calculation.
    public int GetNextFinalPassFrameIndex()
    {
        int index;
        for (index = 0; index < this.frameBlockSize; index++)
        {
            if (finalPassTracker[index] == false)
            {
                return index;
            }
        }

        return index;
    }

    // Returns the global index of the next frame for NIQE and BRISQUE calculation.
    public int GetGlobalNextFinalPassFrameIndex()
    {
        return GetNextFinalPassFrameIndex() + startFrameIndex;
    }

    // Sets NIQe and BRISQUE values for the set relative frame index.
    public bool SetFinalPassValues(double niqe, double brisque, int indexToSet)
    {
        totalBrisque += brisque;
        totalNiqe += niqe;
        finalPassTracker[indexToSet] = true;

        if (indexToSet >= (frameBlockSize - 1))
        {
            finalPassComplete = true;
            avgBrisque = totalBrisque / frameBlockSize;
            avgNiqe = totalNiqe / frameBlockSize;
            return true;
        }

        return false;
    }

    // Returns true if NIQE and BRISQUE values were calculated for every frame within the frameblock.
    public bool GetFinalPassComplete()
    {
        return finalPassComplete;
    }
}