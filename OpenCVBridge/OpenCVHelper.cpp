﻿#include "pch.h"
#include "OpenCVHelper.h"
#include "MemoryBuffer.h"
#include <iostream>
#include <numeric>
using namespace Microsoft::WRL;

using namespace OpenCVBridge;
using namespace Platform;
using namespace Windows::Graphics::Imaging;
using namespace Windows::Storage::Streams;
using namespace Windows::Foundation;

using namespace cv;

OpenCVHelper::OpenCVHelper()
{
}

void OpenCVHelper::ClearDetectedCorners()
{
	detectedCorners.clear();
}

void OpenCVHelper::UpdateChessParameters(int newChessX, int newChessY, float newChessSquareSizeMeters)
{
	bool needToClear = false;

	if (newChessX != chessX) {
		chessX = newChessX;
		needToClear = true;
	}

	if (newChessY != chessY) {
		chessY = newChessY;
		needToClear = true;
	}

	if (abs(newChessSquareSizeMeters - chessSquareSizeMeters) > 0.00001f) {
		chessSquareSizeMeters = newChessSquareSizeMeters;
		needToClear = true;
	}

	if (needToClear) {
		ClearDetectedCorners();
	}
}

void OpenCVHelper::DrawChessboard(SoftwareBitmap^ input, SoftwareBitmap^ output, int chessX, int chessY, float chessSquareSizeMeters, bool saveDetectedCorners) {
	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		return;
	}

	inputImageSize.width = inputMat.cols;
	inputImageSize.height = inputMat.rows;

	UpdateChessParameters(chessX, chessY, chessSquareSizeMeters);

	Mat src_gray;
	cvtColor(inputMat, src_gray, CV_BGRA2GRAY);

	cv::Size patternSize(chessX, chessY);

	std::vector<Point2f> pointBuf;

	bool found = findChessboardCorners(src_gray, patternSize, pointBuf, CV_CALIB_CB_ADAPTIVE_THRESH | CV_CALIB_CB_FAST_CHECK | CV_CALIB_CB_NORMALIZE_IMAGE);

	if (found) {
		cornerSubPix(src_gray, pointBuf, cv::Size(11, 11), cv::Size(-1, -1), TermCriteria(CV_TERMCRIT_EPS + CV_TERMCRIT_ITER, 30, 0.1));
		
		if (saveDetectedCorners) {
			detectedCorners.push_back(pointBuf);
		}

		inputMat.copyTo(outputMat);
		drawChessboardCorners(outputMat, patternSize, pointBuf, found);
	}
	else {
		cvtColor(src_gray, outputMat, CV_GRAY2BGRA);
	}
}

double OpenCVHelper::CalibrateIntrinsics(int maxNumInputFrames)
{
	char s[4096];



	int numFramesToUse = std::min(GetNumDetectedCorners(), maxNumInputFrames);

	
	// come up with a random subset of the corners we've detected, and use that subset for calibration
	std::vector<unsigned int> indices(GetNumDetectedCorners());
	std::iota(indices.begin(), indices.end(), 0);
	std::random_shuffle(indices.begin(), indices.end());

	std::vector<Point3f> obj;
	for (int i = 0; i < chessY; i++) {
		for (int j = 0; j < chessX; j++) {
			obj.push_back(Point3f((float)j * chessSquareSizeMeters, (float)i * chessSquareSizeMeters, 0));
		}
	}

	std::vector<std::vector<Point2f>> imagePoints;
	std::vector<std::vector<Point3f>> objectPoints;

	for (int idx = 0; idx < numFramesToUse; idx++) {
		auto corners = detectedCorners[indices[idx]];	// use shuffled subset of corners
		//auto corners = detectedCorners[idx];			// use first N corners

		objectPoints.push_back(obj);
		imagePoints.push_back(corners);
	}

	Mat cameraMatrix;
	Mat distCoeffs;
	std::vector<Mat> rvecs, tvecs;
	int flags = 0 | CV_CALIB_FIX_K4 | CV_CALIB_FIX_K5;

	double rms = calibrateCamera(objectPoints, imagePoints, inputImageSize, cameraMatrix, distCoeffs, rvecs, tvecs, flags);
	return rms;
}

int OpenCVHelper::GetNumDetectedCorners()
{
	return detectedCorners.size();
}

bool OpenCVHelper::TryConvert(SoftwareBitmap^ from, Mat& convertedMat)
{
	unsigned char* pPixels = nullptr;
	unsigned int capacity = 0;
	if (!GetPointerToPixelData(from, &pPixels, &capacity))
	{
		return false;
	}

	Mat mat(from->PixelHeight,
		from->PixelWidth,
		CV_8UC4, // assume input SoftwareBitmap is BGRA8
		(void*)pPixels);

	// shallow copy because we want convertedMat.data = pPixels
	// don't use .copyTo or .clone
	convertedMat = mat;
	return true;
}

bool OpenCVHelper::GetPointerToPixelData(SoftwareBitmap^ bitmap, unsigned char** pPixelData, unsigned int* capacity)
{
	BitmapBuffer^ bmpBuffer = bitmap->LockBuffer(BitmapBufferAccessMode::ReadWrite);
	IMemoryBufferReference^ reference = bmpBuffer->CreateReference();

	ComPtr<IMemoryBufferByteAccess> pBufferByteAccess;
	if ((reinterpret_cast<IInspectable*>(reference)->QueryInterface(IID_PPV_ARGS(&pBufferByteAccess))) != S_OK)
	{
		return false;
	}

	if (pBufferByteAccess->GetBuffer(pPixelData, capacity) != S_OK)
	{
		return false;
	}
	return true;
}