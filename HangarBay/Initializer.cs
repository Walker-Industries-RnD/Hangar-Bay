using Pariah_Cybersecurity;
using System;
using System.Collections.Generic;
using System.Text;
using WISecureData;
using Pariah_Cybersecurity;

namespace HangarBay
{
    public static class Initializer
    {

        public static async Task InitializeSecureData(SecureData bankName, SecureData dataPhrase, string? bankDirectory = null)
        {

            if (bankDirectory == null)
            {
               bankDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Stride3D Secret Banks");
            }
            SecureData masterKey = DataHandler.DeviceIdentifier.GetUserBoundMasterSecret(dataPhrase.ConvertToString());


            var bankExists = await DataHandler.SecretManager.CheckIfBankExists(bankDirectory, bankName.ConvertToString());

            //If it does, we continue. If not, let's make one!

            if (!bankExists)
            {
                Directory.CreateDirectory(bankDirectory);
                await DataHandler.SecretManager.CreateBank(bankDirectory, bankName.ConvertToString(), null, masterKey.ConvertToString());
            }

        }




    }
}
