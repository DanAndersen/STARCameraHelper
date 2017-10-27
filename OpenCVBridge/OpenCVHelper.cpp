#include "pch.h"
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

		obj.clear();
		for (int i = 0; i < chessY; i++) {
			for (int j = 0; j < chessX; j++) {
				obj.push_back(Point3f((float)j * chessSquareSizeMeters, (float)i * chessSquareSizeMeters, 0));
			}
		}
	}
}

PnPResult OpenCVHelper::FindExtrinsics(SoftwareBitmap^ input, SoftwareBitmap^ output, int chessX, int chessY, float chessSquareSizeMeters, IntrinsicCalibration intrinsics)
{
	PnPResult result;
	result.success = false;


	Mat inputMat, outputMat;
	if (!(TryConvert(input, inputMat) && TryConvert(output, outputMat)))
	{
		return result;
	}

	UpdateChessParameters(chessX, chessY, chessSquareSizeMeters);

	Mat src_gray;
	flip(inputMat, inputMat, 0);
	cvtColor(inputMat, src_gray, CV_BGRA2GRAY);

	cv::Size patternSize(chessX, chessY);

	std::vector<Point2f> pointBuf;

	bool found = findChessboardCorners(src_gray, patternSize, pointBuf, CV_CALIB_CB_ADAPTIVE_THRESH | CV_CALIB_CB_FAST_CHECK | CV_CALIB_CB_NORMALIZE_IMAGE);

	if (found) {
		cornerSubPix(src_gray, pointBuf, cv::Size(11, 11), cv::Size(-1, -1), TermCriteria(CV_TERMCRIT_EPS + CV_TERMCRIT_ITER, 30, 0.1));

		Mat cameraMatrix(cv::Size(3, 3), CV_64F, 0.0);
		cameraMatrix.at<double>(0, 0) = intrinsics.fx;
		cameraMatrix.at<double>(1, 1) = intrinsics.fy;
		cameraMatrix.at<double>(0, 2) = intrinsics.cx;
		cameraMatrix.at<double>(1, 2) = intrinsics.cy;
		cameraMatrix.at<double>(2, 2) = 1.0;

		Mat distCoeffs(cv::Size(1,5), CV_64F, 0.0);
		distCoeffs.at<double>(0) = intrinsics.k1;
		distCoeffs.at<double>(1) = intrinsics.k2;
		distCoeffs.at<double>(2) = intrinsics.p1;
		distCoeffs.at<double>(3) = intrinsics.p2;
		distCoeffs.at<double>(4) = intrinsics.k3;


		Mat rvec, tvec;
		bool success = solvePnP(obj, pointBuf, cameraMatrix, distCoeffs, rvec, tvec);

		// draw chessboard corners
		inputMat.copyTo(outputMat);
		drawChessboardCorners(outputMat, patternSize, pointBuf, found);

		result.success = success;
		result.rvec_0 = rvec.at<double>(0);
		result.rvec_1 = rvec.at<double>(1);
		result.rvec_2 = rvec.at<double>(2);
		result.tvec_0 = tvec.at<double>(0);
		result.tvec_1 = tvec.at<double>(1);
		result.tvec_2 = tvec.at<double>(2);
	}
	else {
		cvtColor(src_gray, outputMat, CV_GRAY2BGRA);
	}
	flip(inputMat, inputMat, 0);
	flip(outputMat, outputMat, 0);
	return result;
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
	flip(inputMat, inputMat, 0);
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
	flip(inputMat, inputMat, 0);
	flip(outputMat, outputMat, 0);
}

IntrinsicCalibration OpenCVHelper::CalibrateIntrinsics(int maxNumInputFrames)
{
	char s[4096];



	int numFramesToUse = std::min(GetNumDetectedCorners(), maxNumInputFrames);

	
	// come up with a random subset of the corners we've detected, and use that subset for calibration
	std::vector<unsigned int> indices(GetNumDetectedCorners());
	std::iota(indices.begin(), indices.end(), 0);
	std::random_shuffle(indices.begin(), indices.end());

	

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
	

	IntrinsicCalibration outputCalib;

	outputCalib.width = inputImageSize.width;
	outputCalib.height = inputImageSize.height;

	outputCalib.fx = cameraMatrix.at<double>(0, 0);
	outputCalib.fy = cameraMatrix.at<double>(1, 1);
	outputCalib.cx = cameraMatrix.at<double>(0, 2);
	outputCalib.cy = cameraMatrix.at<double>(1, 2);
	
	outputCalib.k1 = distCoeffs.at<double>(0);
	outputCalib.k2 = distCoeffs.at<double>(1);
	outputCalib.p1 = distCoeffs.at<double>(2);
	outputCalib.p2 = distCoeffs.at<double>(3);
	outputCalib.k3 = distCoeffs.at<double>(4);
	
	outputCalib.rms = rms;

	return outputCalib;
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