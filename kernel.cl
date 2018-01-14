uint GetBit(uint x, uint y, uint pw, __global uint* second)
{
	return (second[y * pw + (x >> 5)] >> (int)(x & 31)) & 1U; 
}

void BitSet(uint x, uint y, uint pw, __global uint* pattern) 
{
	pattern[y * pw + (x >> 5)] |= 1U << (int)(x & 31); 
}

__kernel void device_function( __global int* a, __global uint* pattern, __global uint* second, uint pw, uint ph, uint xoffset, uint yoffset)
{
	uint idx = get_global_id( 0 );
	uint idy = get_global_id( 1 );
	if(idx == 0 || idx == pw * 32 - 1 || idy == 0 || idy == ph - 1) return;
	uint id = idx + pw * idy;

	uint n = GetBit(idx - 1, idy - 1, pw, second) + GetBit(idx, idy - 1, pw, second) + GetBit(idx + 1, idy - 1, pw, second) + GetBit(idx - 1, idy, pw, second) + GetBit(idx + 1, idy, pw, second) + GetBit(idx - 1, idy + 1, pw, second) + GetBit(idx, idy + 1, pw, second) + GetBit(idx + 1, idy + 1, pw, second);
	if ((GetBit(idx, idy, pw, second) == 1 && n == 2) || n == 3) BitSet(idx, idy, pw, pattern);
}