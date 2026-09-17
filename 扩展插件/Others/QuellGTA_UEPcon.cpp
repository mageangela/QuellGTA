#include <windows.h>
#include <tlhelp32.h>
#include <string>
#include <thread>
#include <chrono>
#include <atomic>

// 全局变量
std::wstring g_pipeName;
std::atomic<bool> g_running(true);
std::atomic<bool> g_shouldExit(false);
HANDLE g_hPipe = INVALID_HANDLE_VALUE;

// 从进程名获取进程ID和可执行文件路径
bool GetProcessInfo(const std::wstring& processName, DWORD& outPid, std::wstring& outFilePath) {
    HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (hSnapshot == INVALID_HANDLE_VALUE) {
        return false;
    }

    PROCESSENTRY32W pe;
    pe.dwSize = sizeof(PROCESSENTRY32W);

    if (Process32FirstW(hSnapshot, &pe)) {
        do {
            if (_wcsicmp(pe.szExeFile, processName.c_str()) == 0) {
                outPid = pe.th32ProcessID;

                HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, outPid);
                if (hProcess) {
                    wchar_t path[MAX_PATH];
                    DWORD size = MAX_PATH;
                    if (QueryFullProcessImageNameW(hProcess, 0, path, &size)) {
                        outFilePath = path;
                        CloseHandle(hProcess);
                        CloseHandle(hSnapshot);
                        return true;
                    }
                    CloseHandle(hProcess);
                }
            }
        } while (Process32NextW(hSnapshot, &pe));
    }

    CloseHandle(hSnapshot);
    return false;
}

// 检查进程是否存在（仅用于判断进程是否已退出）
bool IsProcessRunning(DWORD pid) {
    HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, pid);
    if (hProcess) {
        DWORD exitCode;
        if (GetExitCodeProcess(hProcess, &exitCode)) {
            CloseHandle(hProcess);
            return exitCode == STILL_ACTIVE;
        }
        CloseHandle(hProcess);
    }
    return false;
}

// 强制结束进程
void KillProcess(DWORD pid) {
    HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, pid);
    if (hProcess) {
        TerminateProcess(hProcess, 1);
        CloseHandle(hProcess);
    }
}

// 启动被保护程序
bool StartTargetProcess(const std::wstring& filePath, DWORD& outPid) {
    STARTUPINFOW si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    if (CreateProcessW(filePath.c_str(), NULL, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
        outPid = pi.dwProcessId;
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return true;
    }
    return false;
}

// 检测是否已有守护进程在运行（尝试连接管道）
bool NotifyExistingGuardian() {
    // 尝试打开已存在的管道，如果成功说明已有守护进程
    HANDLE hPipe = CreateFileW(
        g_pipeName.c_str(),
        GENERIC_WRITE,
        0,
        NULL,
        OPEN_EXISTING,
        0,
        NULL);

    if (hPipe == INVALID_HANDLE_VALUE) {
        return false;
    }

    // 发送心跳消息
    const char* msg = "PING";
    DWORD written = 0;
    BOOL result = WriteFile(hPipe, msg, 4, &written, NULL);
    CloseHandle(hPipe);
    return result == TRUE;
}

// 管道监听线程：接收心跳消息
void PipeServerThread() {
    while (g_running) {
        // 创建命名管道
        HANDLE hPipe = CreateNamedPipeW(
            g_pipeName.c_str(),
            PIPE_ACCESS_INBOUND,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1,              // 最多一个实例
            1024,           // 输出缓冲区
            1024,           // 输入缓冲区
            0,              // 默认超时
            NULL);

        if (hPipe == INVALID_HANDLE_VALUE) {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
            continue;
        }

        g_hPipe = hPipe;

        // 等待客户端连接（阻塞）
        BOOL connected = ConnectNamedPipe(hPipe, NULL) ?
            TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);

        if (connected && g_running) {
            // 读取消息
            char buffer[64] = { 0 };
            DWORD bytesRead = 0;
            if (ReadFile(hPipe, buffer, sizeof(buffer) - 1, &bytesRead, NULL) && bytesRead > 0) {
                // 收到心跳消息，重置计时器
                // 通过全局原子变量通知主循环
                // 这里用简单的方式：设置一个标志，由主循环处理
                // 但为了简洁，我们用一个更直接的方式
                extern std::atomic<ULONGLONG> g_lastHeartbeat;
                g_lastHeartbeat = GetTickCount64();
            }
        }

        DisconnectNamedPipe(hPipe);
        CloseHandle(hPipe);
        g_hPipe = INVALID_HANDLE_VALUE;
    }
}

// 全局心跳时间
std::atomic<ULONGLONG> g_lastHeartbeat(0);

int main() {
    // 解析命令行参数
    int argc;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argc < 2) {
        if (argv) LocalFree(argv);
        return 0;
    }

    std::wstring targetProcessName = argv[1];
    LocalFree(argv);

    // 用进程名生成唯一的管道名
    g_pipeName = L"\\\\.\\pipe\\QuellGTA_UEP";

    // 检查是否已有守护进程在运行
    if (NotifyExistingGuardian()) {
        // 已有守护进程，通知它重置计时器后退出
        return 0;
    }

    // 查找目标进程
    DWORD pid = 0;
    std::wstring filePath;
    if (!GetProcessInfo(targetProcessName, pid, filePath)) {
        // 进程不存在，直接退出
        return 0;
    }

    // 初始化心跳时间
    g_lastHeartbeat = GetTickCount64();

    // 启动管道监听线程
    std::thread pipeThread(PipeServerThread);
    pipeThread.detach();

    // 主监控循环
    while (g_running) {
        // 检查进程是否还存在
        if (!IsProcessRunning(pid)) {
            // 进程已退出，检查文件是否存在
            DWORD fileAttr = GetFileAttributesW(filePath.c_str());
            if (fileAttr != INVALID_FILE_ATTRIBUTES && !(fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
                // 文件存在，重启程序
                if (StartTargetProcess(filePath, pid)) {
                    g_lastHeartbeat = GetTickCount64();
                    std::this_thread::sleep_for(std::chrono::seconds(5));
                    continue;
                }
            }
            else {
                // 文件不存在
                MessageBoxW(NULL,
                    L"QuellGTA主程序已经丢失！可能被杀毒软件删除，请调整杀毒软件设置后重新下载运行！\nQuellGTA main program missing! It might have been deleted by antivirus software. Please adjust your antivirus settings and download it again!",
                    L"QuellGTA",
                    MB_OK | MB_ICONERROR);
                g_running = false;
                return 0;
            }
        }

        // 检查心跳超时（5秒）
        ULONGLONG now = GetTickCount64();
        ULONGLONG last = g_lastHeartbeat.load();
        if (now - last >= 5000) {
            // 超时，强制结束进程并重启
            KillProcess(pid);

            // 等待进程完全退出
            for (int i = 0; i < 50 && IsProcessRunning(pid); i++) {
                std::this_thread::sleep_for(std::chrono::milliseconds(100));
            }

            // 检查文件是否存在
            DWORD fileAttr = GetFileAttributesW(filePath.c_str());
            if (fileAttr != INVALID_FILE_ATTRIBUTES && !(fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
                if (StartTargetProcess(filePath, pid)) {
                    g_lastHeartbeat = GetTickCount64();
                    std::this_thread::sleep_for(std::chrono::seconds(5));
                    continue;
                }
            }
            else {
                MessageBoxW(NULL,
                    L"QuellGTA主程序已经丢失！可能被杀毒软件删除，请调整杀毒软件设置后重新下载运行！\nQuellGTA main program missing! It might have been deleted by antivirus software. Please adjust your antivirus settings and download it again!",
                    L"QuellGTA",
                    MB_OK | MB_ICONERROR);
                g_running = false;
                return 0;
            }
        }

        // 每秒检查一次
        std::this_thread::sleep_for(std::chrono::seconds(1));
    }

    return 0;
}