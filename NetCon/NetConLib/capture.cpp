#include "pch.h"
#include "framework.h"

#include "capture.h"
#include <string>
#include <iostream>
#include <stdio.h>  //for system('cls')
#include <io.h>		//for low-level open, read, write
#include <fcntl.h>  //for _O_RDONLY
//#include <sys/types.h>
//#include <sys/stat.h>
#include <thread>
//#include <atomic>
#include <chrono>
#include <conio.h> //fro getch()
#include <csignal>
#include <future>
#include <ctime> //for timestamping files
#include <sstream> //for stringstream in timestamp files
#include <errno.h>

#include "captureThreads.h"
//#include "filters.h"
#include "control.h"
#include "ringBuffer.h"

#include "NetConLib.h"

using namespace std::chrono_literals;

void intHandler(int) {
	if (exitStatus != 0)
		exit(SIGTERM);
	exitStatus = -1;
	std::cout << "Wyslano sygnal SIGTERM, zamykanie programu" << std::endl;
}

void intHandler2(int) {
	if (exitStatus != 0)
		exit(SIGINT);
	exitStatus = -1;
	std::cout << "Wyslano sygnal SIGINT, zamykanie programu" << std::endl;
}

void keyboardInput() {
	while (_getch() != 'x')
		std::this_thread::sleep_for(1ms);
	exitStatus = -1;
}

__declspec(dllexport) int stopCapture() {
	exitStatus = -1;
	return 1;
}

//modified - filename to send_callback
__declspec(dllexport) int startCapture(int port, int (*send_callback)(const char * blob, int blob_size), const int bufSize) {
	std::signal(SIGTERM, intHandler);
	std::signal(SIGINT, intHandler2);

	/*char test[] = "test_text\n";

	for (int i = 0; i < 1000; i++) {
		send_callback(test, sizeof(test));
	}*/
	

	const char* fileName = "naszeRamki.pcap";

	const char* deviceName;

	if (port == 1) {
		deviceName = "\\\\.\\xillybus_read_32_1";
	}
	else if (port == 2) {
		deviceName = "\\\\.\\xillybus_read_32_2";
	}
	else if (port == 3) {
		deviceName = "\\\\.\\xillybus_read_32_3";
	}
	else if (port == 4) {
		deviceName = "\\\\.\\xillybus_read_32_4";
	}
	else {
		throw "Bledny numer portu.";
	}

	//Wczytywanie wartoœci domyœlnych, je¿eli nie zosta³y podane
	if (deviceName == nullptr)
		throw "Brak nazwy strumienia";

	if (bufSize == 0)
		throw "Rozmiar bufora nie mo¿e wynosiæ 0";

	std::string strFileName;
	if (fileName == nullptr || fileName == "") {
		auto currentTime = std::chrono::system_clock::now();
		time_t now = time(0);
		tm* ltm = localtime(&now);
		std::stringstream stringBuffer;
		stringBuffer << "capture_" << (1900 + ltm->tm_year) << "_" << ltm->tm_mon << "_" << ltm->tm_mday << "_" << ltm->tm_hour << "_" << ltm->tm_min << "_" << ltm->tm_sec << "_port" << port << ".pcap";
		//capture_2019.10.5_12:45:10_port1.pcap
		//capture_2019.10.5_12:45:10_port2.pcap
		strFileName = stringBuffer.str().c_str();
	}
	else {
		strFileName = fileName;
	}

	std::cout << "Alokowanie bufora o rozmiarze " << bufSize << " bajtow" << std::endl;
	const char* p_ringBuffer = (const char*)malloc(bufSize);
	if (p_ringBuffer == nullptr)
		return -1;

	int dev = _open(deviceName, _O_RDONLY | _O_BINARY);
	if (dev < 0) {
		std::cout << "Otwieranie strumienia NetFPGA " << deviceName << " nie powiodlo sie." << std::endl;
		exitStatus = -1;
		return -2;
	}
	else
		std::cout << "Otwarcie strumienia NetFPGA " << deviceName << " powiodlo sie.\n" << std::endl;


	//Tworzenie pliku lub jego czyszczenie w którym bêd¹ zapisanie przechwytywane
	int file = _open(strFileName.c_str(), _O_RDWR | _O_SEQUENTIAL | _O_BINARY | _O_CREAT | _O_TRUNC, _S_IREAD | _S_IWRITE);

	if (file < 0) {
		std::cout << "Otwarcie/stworzenie pliku " << strFileName.c_str() << " nie powiodlo sie." << std::endl;
		exitStatus = errno;
		return errno;
	}
	//Wstawianie nag³ówka pliku .pcap z identyfikatorem linka(LINK_TYPE) typu M_PACKET.
	uint32_t header[] = { 0xA1B23C4D, 0x00040002, 0x00000000, 0x00000000, 0xFFFFFFFF, 0x00000112 };
	int bytesToWrite = sizeof(header);
	while (bytesToWrite > 0) {
		int ret = _write(file, (char*)header + (sizeof(header) - bytesToWrite), bytesToWrite);
		if (ret > 0)
			bytesToWrite -= ret;
	}

	ringBuffer ringBuf(p_ringBuffer, bufSize, 1);

	std::cout << "Nacisnij klawisz ""x"" aby zatrzymac przechwytywanie i zamknac program.\n";

	//Rozpoczynanie w¹tka odbieraj¹cego dane, zapisuj¹cego dane oraz reportuj¹cego dane
	std::thread readThread(readDevice, dev, std::ref(ringBuf)); //std::ref
	//std::thread writeThread(writeToFile, file, std::ref(ringBuf));
	std::thread reportThread(reportBuffer, std::ref(ringBuf));

	//Nasz nowy w¹tek wysy³aj¹cy dane do C#
	
	std::thread sendToListenerThread(sendToListener,file, send_callback, std::ref(ringBuf));

	//std::thread waitForKeyboardInput(keyboardInput);

	sendRequest(port, CAPTURE_SWITCH, true);
	while (exitStatus == 0)
		std::this_thread::sleep_for(1ms);

	sendRequest(port, CAPTURE_SWITCH, false);
	std::this_thread::sleep_for(100ms);

	std::cout << "\nZamykanie programu w toku..." << std::endl;

	std::cout << "Zamykanie watku writeThread." << std::endl;
	//writeThread.join();
	_close(file);

	//do³¹czenie naszego w¹tku
	sendToListenerThread.join();
	std::cout << "Zamykanie watku reportThread." << std::endl;
	reportThread.join();

	
	
	//Workaround: Czekanie z timeoutem na zamkniecie watku readThread, jednakze bedzie on praktycznie zawsze zablokowany przez blokujacego read()
	//Problem nie jest krytyczny bo wszystko zosta³o ju¿ odczytane i zapisane
	std::cout << "Zamykanie watku readThread." << std::endl;


	readThread.~thread();

	//auto future = std::async(std::launch::async, &std::thread::join, &readThread);
	//if (future.wait_for(std::chrono::seconds(1)) == std::future_status::timeout) {
	//	//raise(SIGTERM);
	//}

	free((void*)p_ringBuffer);
	std::cout << "Program zamkniety." << std::endl;
	return 0;
}