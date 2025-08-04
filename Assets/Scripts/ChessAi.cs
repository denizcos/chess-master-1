using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;

public class ChessAI : MonoBehaviour
{
    public enum AIDifficulty { Easy = 2, Medium = 4, Hard = 6 }

    // AI Settings - HIDDEN from inspector
    private AIDifficulty currentDifficulty = AIDifficulty.Easy;
    private bool isEngineReady = false;

    // References - HIDDEN from inspector  
    private ChessRules chessRules;

    // Stockfish process management
    private Process stockfishProcess;
    private Queue<string> outputQueue = new Queue<string>();
    private Queue<string> commandQueue = new Queue<string>();
    private bool isProcessingCommand = false;
    private bool isProcessingAIMove = false;
    private string lastBestMove = "";

    // Events
    public System.Action<string> OnAIMoveReady;
    public System.Action<string> OnAIThinking;
    public System.Action<string> OnEngineStatus;
    public System.Action<string> OnEngineError;

    void Start()
    {
        StartStockfish();
    }

    void Update()
    {
        ProcessOutputQueue();
        ProcessCommandQueue();
    }

    #region Stockfish Initialization

    void StartStockfish()
    {
        try
        {
            string stockfishPath = GetStockfishPath();

            if (!File.Exists(stockfishPath))
            {
                OnEngineError?.Invoke($"Stockfish not found at: {stockfishPath}");
                UnityEngine.Debug.LogError($"Stockfish not found. Please place stockfish executable in Assets/StreamingAssets/ folder");
                return;
            }

            stockfishProcess = new Process();

            stockfishProcess.StartInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(stockfishPath)
            };

            stockfishProcess.OutputDataReceived += OnOutputDataReceived;
            stockfishProcess.ErrorDataReceived += OnErrorDataReceived;

            stockfishProcess.Start();
            stockfishProcess.BeginOutputReadLine();
            stockfishProcess.BeginErrorReadLine();

            StartCoroutine(InitializeUCI());
        }
        catch (Exception ex)
        {
            OnEngineError?.Invoke($"Failed to start Stockfish: {ex.Message}");
        }
    }

    string GetStockfishPath()
    {
        string fileName;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        fileName = "stockfish.exe";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        fileName = "stockfish-mac";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        fileName = "stockfish-linux";
#elif UNITY_ANDROID
        fileName = "stockfish-android";
#else
        fileName = "stockfish.exe";
#endif

        return Path.Combine(Application.streamingAssetsPath, fileName);
    }

    IEnumerator InitializeUCI()
    {
        SendCommand("uci");

        float timeout = 10f;
        float elapsed = 0f;

        while (!isEngineReady && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!isEngineReady)
        {
            OnEngineError?.Invoke("Failed to initialize UCI");
            yield break;
        }

        SendCommand($"setoption name Skill Level value {(int)currentDifficulty}");
        yield return new WaitForSeconds(0.1f);

        SendCommand("ucinewgame");
        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(WaitForReady());

        UnityEngine.Debug.Log("Stockfish ready!");
        OnEngineStatus?.Invoke("Stockfish engine ready!");
    }

    IEnumerator WaitForReady()
    {
        isProcessingCommand = true;
        SendCommand("isready");

        float timeout = 5f;
        float elapsed = 0f;

        while (isProcessingCommand && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (isProcessingCommand)
        {
            OnEngineError?.Invoke("Engine not responding to isready");
        }
    }

    #endregion

    #region Communication

    void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            lock (outputQueue)
            {
                outputQueue.Enqueue(e.Data);
            }
        }
    }

    void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            UnityEngine.Debug.LogError($"Stockfish Error: {e.Data}");
            OnEngineError?.Invoke($"Stockfish Error: {e.Data}");
        }
    }

    void ProcessOutputQueue()
    {
        lock (outputQueue)
        {
            while (outputQueue.Count > 0)
            {
                string line = outputQueue.Dequeue();
                ProcessEngineLine(line);
            }
        }
    }

    void ProcessEngineLine(string line)
    {
        UnityEngine.Debug.Log($"Stockfish: {line}");

        if (line.StartsWith("uciok"))
        {
            isEngineReady = true;
        }
        else if (line.StartsWith("readyok"))
        {
            isProcessingCommand = false;
        }
        else if (line.StartsWith("bestmove"))
        {
            string[] parts = line.Split(' ');
            if (parts.Length >= 2)
            {
                lastBestMove = parts[1];
                if (!string.IsNullOrEmpty(lastBestMove) && lastBestMove != "(none)")
                {
                    OnAIMoveReady?.Invoke(lastBestMove);
                }
            }
            isProcessingCommand = false;
            isProcessingAIMove = false;
        }
        else if (line.StartsWith("info"))
        {
            // Handle analysis info if needed
        }
    }

    void ProcessCommandQueue()
    {
        if (!isProcessingCommand && commandQueue.Count > 0)
        {
            string command = commandQueue.Dequeue();
            SendCommandImmediate(command);
        }
    }

    public void SendCommand(string command)
    {
        commandQueue.Enqueue(command);
    }

    void SendCommandImmediate(string command)
    {
        try
        {
            if (stockfishProcess != null && !stockfishProcess.HasExited)
            {
                stockfishProcess.StandardInput.WriteLine(command);
                stockfishProcess.StandardInput.Flush();
                UnityEngine.Debug.Log($"Sent: {command}");

                if (command.StartsWith("go") || command == "isready")
                {
                    isProcessingCommand = true;
                }
            }
        }
        catch (Exception ex)
        {
            OnEngineError?.Invoke($"Error sending command: {ex.Message}");
        }
    }

    #endregion

    #region AI Move Generation

    public void GetAIMove()
    {
        if (!isEngineReady || isProcessingAIMove || chessRules == null)
        {
            OnEngineError?.Invoke("AI not ready or no chess rules reference");
            return;
        }

        StartCoroutine(GetAIMoveCoroutine());
    }

    IEnumerator GetAIMoveCoroutine()
    {
        isProcessingAIMove = true;
        OnAIThinking?.Invoke("AI is thinking...");

        string fen = chessRules.GetCurrentFEN();
        UnityEngine.Debug.Log($"Sending position: {fen}");

        SendCommandImmediate($"position fen {fen}");
        yield return new WaitForSeconds(0.3f);

        SendCommandImmediate("isready");

        float timeout = 5f;
        float elapsed = 0f;
        bool engineReady = false;

        while (elapsed < timeout && !engineReady)
        {
            if (!isProcessingCommand)
            {
                engineReady = true;
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!engineReady)
        {
            UnityEngine.Debug.LogWarning("Engine not ready, proceeding anyway...");
        }

        int thinkTime = currentDifficulty switch
        {
            AIDifficulty.Easy => 1800,
            AIDifficulty.Medium => 2600,
            AIDifficulty.Hard => 3600,
            _ => 3000
        };

        UnityEngine.Debug.Log($"Sending go command with {thinkTime}ms think time");
        SendCommandImmediate($"go movetime {thinkTime}");
    }

    #endregion

    // Public methods to set references and check status
    public void SetChessRules(ChessRules rules) { chessRules = rules; }
    public AIDifficulty GetCurrentDifficulty() { return currentDifficulty; }
    public bool GetIsEngineReady() { return isEngineReady; }

    public void SetDifficulty(AIDifficulty difficulty)
    {
        currentDifficulty = difficulty;
        if (isEngineReady)
        {
            SendCommand($"setoption name Skill Level value {(int)currentDifficulty}");
        }
    }

    public void SetDifficulty(int difficultyIndex)
    {
        switch (difficultyIndex)
        {
            case 0: SetDifficulty(AIDifficulty.Easy); break;
            case 1: SetDifficulty(AIDifficulty.Medium); break;
            case 2: SetDifficulty(AIDifficulty.Hard); break;
            default: SetDifficulty(AIDifficulty.Easy); break;
        }
    }

    public void ResetAI()
    {
        isProcessingAIMove = false;
        if (isEngineReady)
        {
            SendCommand("ucinewgame");
        }
    }

    public bool IsReady()
    {
        return isEngineReady && !isProcessingAIMove;
    }

    public bool IsThinking()
    {
        return isProcessingAIMove;
    }


    #region Cleanup

    void OnDestroy()
    {
        try
        {
            if (stockfishProcess != null)
            {
                if (!stockfishProcess.HasExited)
                {
                    SendCommandImmediate("quit");

                    if (!stockfishProcess.WaitForExit(2000))
                    {
                        stockfishProcess.Kill();
                    }
                }
                stockfishProcess.Dispose();
                stockfishProcess = null;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"Error disposing Stockfish: {e.Message}");
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Test Stockfish Connection")]
    public void TestStockfishConnection()
    {
        UnityEngine.Debug.Log("=== STOCKFISH CONNECTION TEST ===");
        UnityEngine.Debug.Log($"Stockfish Path: {GetStockfishPath()}");
        UnityEngine.Debug.Log($"File Exists: {File.Exists(GetStockfishPath())}");
        UnityEngine.Debug.Log($"Engine Ready: {isEngineReady}");
        UnityEngine.Debug.Log($"Current Difficulty: {currentDifficulty}");

        if (stockfishProcess != null)
        {
            try
            {
                UnityEngine.Debug.Log($"Process Running: {!stockfishProcess.HasExited}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log($"Error checking process status: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.Log("No Stockfish process");
        }
    }

    [ContextMenu("Test AI Move")]
    public void TestAIMove()
    {
        if (isEngineReady)
        {
            GetAIMove();
        }
        else
        {
            UnityEngine.Debug.LogWarning("Stockfish not ready!");
        }
    }

    #endregion
}