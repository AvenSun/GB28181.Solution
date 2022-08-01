/*
Copyright (c) 2011 Stanislav Vitvitskiy

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the "Software"), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify,
merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE
OR OTHER DEALINGS IN THE SOFTWARE.
*/
using Helpers;
namespace mp4parser.h264.model
{

    public class HRDParameters
    {
        public int bit_rate_scale { get; set; }
        public int cpb_size_scale { get; set; }
        public int[] bit_rate_value_minus1 { get; set; }
        public int[] cpb_size_value_minus1 { get; set; }
        public bool[] cbr_flag { get; set; }
        public int initial_cpb_removal_delay_length_minus1 { get; set; }
        public int cpb_removal_delay_length_minus1 { get; set; }
        public int dpb_output_delay_length_minus1 { get; set; }
        public int time_offset_length { get; set; }

        public int Cpb_cnt_minus1 { get; set; }

        public override string ToString()
        {

            return "HRDParameters{" + "cpb_cnt_minus1=" + Cpb_cnt_minus1 + ", bit_rate_scale=" + bit_rate_scale + ", cpb_size_scale=" + cpb_size_scale + ", bit_rate_value_minus1=" + bit_rate_value_minus1.ConstituteString() + ", cpb_size_value_minus1=" + cpb_size_value_minus1.ConstituteString() + ", cbr_flag=" + cbr_flag.ConstituteString() + ", initial_cpb_removal_delay_length_minus1=" + initial_cpb_removal_delay_length_minus1 + ", cpb_removal_delay_length_minus1=" + cpb_removal_delay_length_minus1 + ", dpb_output_delay_length_minus1=" + dpb_output_delay_length_minus1 + ", time_offset_length=" + time_offset_length + '}';
        }
    }

}