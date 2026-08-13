#include <windows.h>
#include <tlhelp32.h>
#include <string>
#include <thread>
#include <chrono>

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
            // 比较进程名（不区分大小写）
            if (_wcsicmp(pe.szExeFile, processName.c_str()) == 0) {
                outPid = pe.th32ProcessID;

                // 通过进程ID获取可执行文件路径
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

// 检查进程是否存在
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

int main() {
    // 解析命令行参数（获取要监控的进程名）
    int argc;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argc < 2) {
        if (argv) LocalFree(argv);
        return 0;
    }

    std::wstring targetProcessName = argv[1];
    LocalFree(argv);

    // 首先尝试查找进程
    DWORD pid = 0;
    std::wstring filePath;
    if (!GetProcessInfo(targetProcessName, pid, filePath)) {
        // 进程不存在，直接退出
        return 0;
    }

    // 主监控循环
    while (true) {
        // 检查进程是否存在
        if (!IsProcessRunning(pid)) {
            // 进程不存在，检查文件是否存在
            DWORD fileAttr = GetFileAttributesW(filePath.c_str());
            if (fileAttr != INVALID_FILE_ATTRIBUTES && !(fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
                // 文件存在，启动程序
                STARTUPINFOW si = { sizeof(si) };
                PROCESS_INFORMATION pi;
                if (CreateProcessW(filePath.c_str(), NULL, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi)) {
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                    // 启动后等待5秒再继续检查
                    std::this_thread::sleep_for(std::chrono::seconds(5));
                    continue;
                }
            }
            else {
                // 文件不存在，弹出错误对话框
                MessageBoxW(NULL,
                    L"QuellGTA主程序已经丢失！可能被杀毒软件删除，请调整杀毒软件设置后重新下载运行！\nQuellGTA main program missing! It might have been deleted by antivirus software. Please adjust your antivirus settings and download it again!",
                    L"QuellGTA",
                    MB_OK | MB_ICONERROR);
                return 0;
            }
        }

        // 休眠1秒后继续检查
        std::this_thread::sleep_for(std::chrono::seconds(1));
    }

    return 0;
}