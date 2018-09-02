using System;
using System.Collections.Generic;
using System.Linq;

namespace tclsh
{
    static class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("puts result is [sum 1 [lindex {1 2 3} 1]]\nputs this is nextline".Evaluate());
            //Console.WriteLine("set a 123\nputs $a".Evaluate());
            //new SimpleTCL().Evaluate("\n\n\rset a 123\n#this is a comment.\nputs $a");
            Console.WriteLine(new SimpleTCL().Evaluate(
                @"set str1 abc
                proc putsfirst {str1,str2} {puts $str1}
                putsfirst 123 234
                puts $str1"
                ));
        }
    }
}