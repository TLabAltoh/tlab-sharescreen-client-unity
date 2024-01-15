#pragma once

/**
* DCT.cginc
*/

#include "TPEGCommon.cginc"
#include "DCTCommon.cginc"

groupshared float BlockY[BLOCK_SIZE];
groupshared float BlockCr[BLOCK_SIZE];
groupshared float BlockCb[BLOCK_SIZE];

inline void CS8x8IDCT_Butterfly_Y(uint Vect0, uint Step) {
	uint Vect1 = Vect0 + Step;
	uint Vect2 = Vect1 + Step;
	uint Vect3 = Vect2 + Step;
	uint Vect4 = Vect3 + Step;
	uint Vect5 = Vect4 + Step;
	uint Vect6 = Vect5 + Step;
	uint Vect7 = Vect6 + Step;

	float Y04P = BlockY[Vect0] + BlockY[Vect4];
	float Y2b6eP = C_b * BlockY[Vect2] + C_e * BlockY[Vect6];

	float Y04P2b6ePP = Y04P + Y2b6eP;
	float Y04P2b6ePM = Y04P - Y2b6eP;
	float Y7f1aP3c5dPP = C_f * BlockY[Vect7] + C_a * BlockY[Vect1] + C_c * BlockY[Vect3] + C_d * BlockY[Vect5];
	float Y7a1fM3d5cMP = C_a * BlockY[Vect7] - C_f * BlockY[Vect1] + C_d * BlockY[Vect3] - C_c * BlockY[Vect5];

	float Y04M = BlockY[Vect0] - BlockY[Vect4];
	float Y2e6bM = C_e * BlockY[Vect2] - C_b * BlockY[Vect6];

	float Y04M2e6bMP = Y04M + Y2e6bM;
	float Y04M2e6bMM = Y04M - Y2e6bM;
	float Y1c7dM3f5aPM = C_c * BlockY[Vect1] - C_d * BlockY[Vect7] - C_f * BlockY[Vect3] - C_a * BlockY[Vect5];
	float Y1d7cP3a5fMM = C_d * BlockY[Vect1] + C_c * BlockY[Vect7] - C_a * BlockY[Vect3] + C_f * BlockY[Vect5];

	BlockY[Vect0] = C_norm * (Y04P2b6ePP + Y7f1aP3c5dPP);
	BlockY[Vect7] = C_norm * (Y04P2b6ePP - Y7f1aP3c5dPP);
	BlockY[Vect4] = C_norm * (Y04P2b6ePM + Y7a1fM3d5cMP);
	BlockY[Vect3] = C_norm * (Y04P2b6ePM - Y7a1fM3d5cMP);

	BlockY[Vect1] = C_norm * (Y04M2e6bMP + Y1c7dM3f5aPM);
	BlockY[Vect5] = C_norm * (Y04M2e6bMM - Y1d7cP3a5fMM);
	BlockY[Vect2] = C_norm * (Y04M2e6bMM + Y1d7cP3a5fMM);
	BlockY[Vect6] = C_norm * (Y04M2e6bMP - Y1c7dM3f5aPM);
}

inline void CS8x8IDCT_Butterfly_Cr(uint Vect0, uint Step) {
	uint Vect1 = Vect0 + Step;
	uint Vect2 = Vect1 + Step;
	uint Vect3 = Vect2 + Step;
	uint Vect4 = Vect3 + Step;
	uint Vect5 = Vect4 + Step;
	uint Vect6 = Vect5 + Step;
	uint Vect7 = Vect6 + Step;

	float Y04P = BlockCr[Vect0] + BlockCr[Vect4];
	float Y2b6eP = C_b * BlockCr[Vect2] + C_e * BlockCr[Vect6];

	float Y04P2b6ePP = Y04P + Y2b6eP;
	float Y04P2b6ePM = Y04P - Y2b6eP;
	float Y7f1aP3c5dPP = C_f * BlockCr[Vect7] + C_a * BlockCr[Vect1] + C_c * BlockCr[Vect3] + C_d * BlockCr[Vect5];
	float Y7a1fM3d5cMP = C_a * BlockCr[Vect7] - C_f * BlockCr[Vect1] + C_d * BlockCr[Vect3] - C_c * BlockCr[Vect5];

	float Y04M = BlockCr[Vect0] - BlockCr[Vect4];
	float Y2e6bM = C_e * BlockCr[Vect2] - C_b * BlockCr[Vect6];

	float Y04M2e6bMP = Y04M + Y2e6bM;
	float Y04M2e6bMM = Y04M - Y2e6bM;
	float Y1c7dM3f5aPM = C_c * BlockCr[Vect1] - C_d * BlockCr[Vect7] - C_f * BlockCr[Vect3] - C_a * BlockCr[Vect5];
	float Y1d7cP3a5fMM = C_d * BlockCr[Vect1] + C_c * BlockCr[Vect7] - C_a * BlockCr[Vect3] + C_f * BlockCr[Vect5];

	BlockCr[Vect0] = C_norm * (Y04P2b6ePP + Y7f1aP3c5dPP);
	BlockCr[Vect7] = C_norm * (Y04P2b6ePP - Y7f1aP3c5dPP);
	BlockCr[Vect4] = C_norm * (Y04P2b6ePM + Y7a1fM3d5cMP);
	BlockCr[Vect3] = C_norm * (Y04P2b6ePM - Y7a1fM3d5cMP);

	BlockCr[Vect1] = C_norm * (Y04M2e6bMP + Y1c7dM3f5aPM);
	BlockCr[Vect5] = C_norm * (Y04M2e6bMM - Y1d7cP3a5fMM);
	BlockCr[Vect2] = C_norm * (Y04M2e6bMM + Y1d7cP3a5fMM);
	BlockCr[Vect6] = C_norm * (Y04M2e6bMP - Y1c7dM3f5aPM);
}

inline void CS8x8IDCT_Butterfly_Cb(uint Vect0, uint Step) {
	uint Vect1 = Vect0 + Step;
	uint Vect2 = Vect1 + Step;
	uint Vect3 = Vect2 + Step;
	uint Vect4 = Vect3 + Step;
	uint Vect5 = Vect4 + Step;
	uint Vect6 = Vect5 + Step;
	uint Vect7 = Vect6 + Step;

	float Y04P = BlockCb[Vect0] + BlockCb[Vect4];
	float Y2b6eP = C_b * BlockCb[Vect2] + C_e * BlockCb[Vect6];

	float Y04P2b6ePP = Y04P + Y2b6eP;
	float Y04P2b6ePM = Y04P - Y2b6eP;
	float Y7f1aP3c5dPP = C_f * BlockCb[Vect7] + C_a * BlockCb[Vect1] + C_c * BlockCb[Vect3] + C_d * BlockCb[Vect5];
	float Y7a1fM3d5cMP = C_a * BlockCb[Vect7] - C_f * BlockCb[Vect1] + C_d * BlockCb[Vect3] - C_c * BlockCb[Vect5];

	float Y04M = BlockCb[Vect0] - BlockCb[Vect4];
	float Y2e6bM = C_e * BlockCb[Vect2] - C_b * BlockCb[Vect6];

	float Y04M2e6bMP = Y04M + Y2e6bM;
	float Y04M2e6bMM = Y04M - Y2e6bM;
	float Y1c7dM3f5aPM = C_c * BlockCb[Vect1] - C_d * BlockCb[Vect7] - C_f * BlockCb[Vect3] - C_a * BlockCb[Vect5];
	float Y1d7cP3a5fMM = C_d * BlockCb[Vect1] + C_c * BlockCb[Vect7] - C_a * BlockCb[Vect3] + C_f * BlockCb[Vect5];

	BlockCb[Vect0] = C_norm * (Y04P2b6ePP + Y7f1aP3c5dPP);
	BlockCb[Vect7] = C_norm * (Y04P2b6ePP - Y7f1aP3c5dPP);
	BlockCb[Vect4] = C_norm * (Y04P2b6ePM + Y7a1fM3d5cMP);
	BlockCb[Vect3] = C_norm * (Y04P2b6ePM - Y7a1fM3d5cMP);

	BlockCb[Vect1] = C_norm * (Y04M2e6bMP + Y1c7dM3f5aPM);
	BlockCb[Vect5] = C_norm * (Y04M2e6bMM - Y1d7cP3a5fMM);
	BlockCb[Vect2] = C_norm * (Y04M2e6bMM + Y1d7cP3a5fMM);
	BlockCb[Vect6] = C_norm * (Y04M2e6bMP - Y1c7dM3f5aPM);
}