using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System;
using UnityEngine;
using UnityEditor;

// A class to interface with the Python process to caalculate NIQE and BRISQUe values of encoded frameblocks.
public class PythonInterface : MonoBehaviour
{
    // Variables for tracking the process of calculating NIQE and BRISQUE vaalues.
    List<FrameBlock> EncodedFrameBlocksIndexList = new List<FrameBlock>();
    FrameBlock currentFrameBlock;
    int finalPassFrameIndex;
    bool calculationEnabled = false;
    bool frameBlockFinished = true;

    // Holds the text of the placeholder line in the unmodified Python code.
    private readonly string codePlaceholderString = "REPLACEMENT_LINE_PLACEHOLDER";
    // Path to the python code to be modified.
    private string pathToPythonCode;
    // Path to the modified Python code.
    private string pathToModifiedPythonCode;

    // DirectoryInfo for the path to the modified Python code.
    DirectoryInfo pythonTextualInfo;
    // Process for Python code execution.
    Process pythonProcess;
    // Contains the standard output of the Python process.
    string pythonPorcessOutput;

    // Update is called once per frame.
    void Update()
    {
        if (calculationEnabled)
        {
            // If the current frameblock is finished move to the next uncompleted frameblock.
            if (frameBlockFinished)
            {
                EncodedFrameBlocksIndexList = gameObject.GetComponent<FfmpegHandler>().GetEncodedFrameBlocksIndexList();
                for (int i = 0; i < EncodedFrameBlocksIndexList.Count; i++)
                {
                    currentFrameBlock = EncodedFrameBlocksIndexList[i];

                    if (!currentFrameBlock.GetFinalPassComplete())
                    {
                        frameBlockFinished = false;
                        break;
                    }
                }
            }

            // Calculate NIQE and BRISQUe values for every frame of the current frameblock.
            if (!frameBlockFinished)
            {
                finalPassFrameIndex = currentFrameBlock.GetNextFinalPassFrameIndex();
                int globalIndexOfFrameToAnalyze = currentFrameBlock.GetGlobalNextFinalPassFrameIndex();
                string pythonScriptLineReplacament = "imgPath = \"" + Application.streamingAssetsPath + "/EncodedFrames/frame" + globalIndexOfFrameToAnalyze + ".png\"\n";
                string pythonCode = File.ReadAllText(pathToPythonCode);
                pythonCode = pythonCode.Replace(codePlaceholderString, pythonScriptLineReplacament);

                File.WriteAllText(pathToModifiedPythonCode, pythonCode);

                pythonProcess.Start();
                pythonPorcessOutput = pythonProcess.StandardOutput.ReadToEnd();

                string[] calculationResults = pythonPorcessOutput.Split('|');

                double brisque = Convert.ToDouble(calculationResults[0]);
                double niqe = Convert.ToDouble(calculationResults[1]);

                if (currentFrameBlock.SetFinalPassValues(brisque, niqe, finalPassFrameIndex))
                {
                    frameBlockFinished = true;
                }
            }
        }
    }

    // Called when the user turns NIQE and BRISQUE calculation ON via the application's UI.
    public void UpdateCalculationEnabled(int dropdownValue)
    {
        if (dropdownValue == 1)
        {
            calculationEnabled = true;
            frameBlockFinished = true;
        }
        else
        {
            calculationEnabled = false;
        }
    }

    // Start is called before the first frame update.
    void Start()
    {
        pathToPythonCode = (Application.streamingAssetsPath + "/PythonScripts/PythonCode.txt").Replace("/", "\\");
        pathToModifiedPythonCode = (Application.streamingAssetsPath + "/PythonCode/PythonCodeMod.txt").Replace("/", "\\");

        Directory.CreateDirectory((Application.streamingAssetsPath + "/PythonCode/").Replace("/", "\\"));

        if (File.Exists(pathToModifiedPythonCode))
        {
            File.Delete(pathToModifiedPythonCode);
        }

        pythonProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = (Application.streamingAssetsPath + "/Python37/python.exe").Replace("/", "\\"),
                Arguments = ("\"" + Application.streamingAssetsPath + "/PythonCode/PythonCodeMod.txt\"").Replace("/", "\\"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };
    }
}
