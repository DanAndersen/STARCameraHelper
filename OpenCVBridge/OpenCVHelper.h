#pragma once
#include <opencv2\core\core.hpp>
#include <opencv2\imgproc\imgproc.hpp>
#include <opencv2\video.hpp>
#include <opencv2\calib3d\calib3d.hpp>

using namespace Windows::Foundation::Collections;

namespace OpenCVBridge 
{

	public value struct IntrinsicCalibration
	{
		// image size
		int width;
		int height;

		// variables for camera matrix
		double fx;
		double fy;
		double cx;
		double cy;

		// distortion coefficients
		double k1;
		double k2;
		double p1;
		double p2;
		double k3;

		// error from calibration
		double rms;
	};

	public value struct PnPResult 
	{
		bool success;

		double rvec_0;
		double rvec_1;
		double rvec_2;

		double tvec_0;
		double tvec_1;
		double tvec_2;
	};

	public ref class OpenCVHelper sealed
	{
	public:
		OpenCVHelper();

		// Image processing operators

		void DrawChessboard(
			Windows::Graphics::Imaging::SoftwareBitmap^ input,
			Windows::Graphics::Imaging::SoftwareBitmap^ output,
			int chessX,
			int chessY,
			float chessSquareSizeMeters,
			bool saveDetectedCorners);

		void ClearDetectedCorners();

		int GetNumDetectedCorners();

		IVector<float>^ GetCurrentPointXYs();

		IntrinsicCalibration CalibrateIntrinsics(int maxNumInputFrames);

		PnPResult FindExtrinsics(Windows::Graphics::Imaging::SoftwareBitmap^ input,
			Windows::Graphics::Imaging::SoftwareBitmap^ output,
			int chessX,
			int chessY,
			float chessSquareSizeMeters, IntrinsicCalibration intrinsics);
	private:
		std::vector<std::vector<cv::Point2f>> detectedCorners;
		int chessX;
		int chessY;
		float chessSquareSizeMeters;

		cv::Size inputImageSize;

		std::vector<cv::Point3f> obj;	// object points (valid for the latest chess dimensions)

		// helper functions for getting a cv::Mat from SoftwareBitmap
		bool GetPointerToPixelData(Windows::Graphics::Imaging::SoftwareBitmap^ bitmap,
			unsigned char** pPixelData, unsigned int* capacity);

		bool TryConvert(Windows::Graphics::Imaging::SoftwareBitmap^ from, cv::Mat& convertedMat);

		void UpdateChessParameters(int newChessX, int newChessY, float newChessSquareSizeMeters);

		std::vector<float> _currentPointXYs;
	};
}