#pragma once
#include <opencv2\core\core.hpp>
#include <opencv2\imgproc\imgproc.hpp>
#include <opencv2\video.hpp>
#include <opencv2\calib3d\calib3d.hpp>

namespace OpenCVBridge 
{
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

		double CalibrateIntrinsics(int maxNumInputFrames);
	private:
		std::vector<std::vector<cv::Point2f>> detectedCorners;
		int chessX;
		int chessY;
		float chessSquareSizeMeters;

		cv::Size inputImageSize;

		// helper functions for getting a cv::Mat from SoftwareBitmap
		bool GetPointerToPixelData(Windows::Graphics::Imaging::SoftwareBitmap^ bitmap,
			unsigned char** pPixelData, unsigned int* capacity);

		bool TryConvert(Windows::Graphics::Imaging::SoftwareBitmap^ from, cv::Mat& convertedMat);

		void UpdateChessParameters(int newChessX, int newChessY, float newChessSquareSizeMeters);
	};
}