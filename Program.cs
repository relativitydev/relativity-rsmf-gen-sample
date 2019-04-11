using System;
using System.IO;
using RSMFGenLib;

/* ----------------------------------------------------------------------------
 * <copyright file="Program.cs" company="Relativity ODA LLC">
 *  © Relativity All Rights Reserved.
 * </copyright>
 *----------------------------------------------------------------------------
*/

namespace RSMFGen
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length < 2)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("RSMFGen <Input directory> <Output RSMF file> -validate");                
                Console.WriteLine("Input directory should be a directory that contains an rsmf_manifest.json file and any attachments it references.");
                Console.WriteLine("Output RSMF file is the file where the RSMF data will be written.");
                Console.WriteLine("Validation on the Zip layer is performed if -validate is specified.  On validation error, no RSMF is created.");
                Console.WriteLine("Output RSMF should not be created in Input directory.");

                return;
            }
            /*
             * This sample application is a simple wrapper around the RSMFGenerator class.             
             * Validation of the input directory is done by the class, but output location still
             * needs validation.
             */
            if(Directory.Exists(Path.GetDirectoryName(args[1])) == false)
            {
                Console.WriteLine($"Output directory {args[1]} doesn't exist.");
                return;
            }

            var rsmf = new RSMFGenerator();
            try
            {
                /*
                 * By populating the custodian information, the "From" address field of the EML will be generated.
                 */
                rsmf.CustodianDisplay = "Relativity";
                rsmf.CustodianEmail = "support@relativity.com";
                
                if(args.Length > 2 && args[2].Equals("-validate", StringComparison.OrdinalIgnoreCase))
                {
                    /*
                     * Validating affects the peformance of creating an RSMF.  If the JSON has already been validated before
                     * running this process then further validating may not be necessary.
                     */
                    rsmf.ValidateZip = true;
                }
                rsmf.GenerateRSMF(new System.IO.DirectoryInfo(args[0]), new System.IO.FileInfo(args[1]));
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("Exception caught attempting to create RSMF file.");
                Console.Error.WriteLine(ex.Message);
            }

        }
    }
}
