using Microsoft.UpdateServices.Administration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WSUSApprove.UpdateServices.AdministrationApi {
    public class AdministrationApi {
        private CultureInfo currentCulture;
        private IUpdateServer currentUpdateServer;

        public AdministrationApi(CultureInfo culture, IUpdateServer updateServer) {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));
            if (updateServer == null)
                throw new ArgumentNullException(nameof(updateServer));
            this.currentCulture = culture;
            this.currentUpdateServer = updateServer;
            this.SetServerCulture();
        }
        private void SetServerCulture() {
            string lowerInvariant = this.CurrentCulture.Name.ToLowerInvariant();
            if (this.TrySetServerCulture(lowerInvariant) || (lowerInvariant == "zh-mo" || lowerInvariant == "zh-sg") && this.TrySetServerCulture("zh-cn") || lowerInvariant == "zh-hk" && this.TrySetServerCulture("zh-tw"))
                return;
            this.TrySetServerCulture(this.currentCulture.Parent.Name);
        }
        private bool TrySetServerCulture(string culture) {
            try {
                this.CurrentUpdateServer.PreferredCulture = culture.ToLowerInvariant();
                return true;
            } catch (ArgumentOutOfRangeException) {
                return false;
            }
        }
        public IUpdateServer CurrentUpdateServer {
            get {
                return this.currentUpdateServer;
            }
        }
        public CultureInfo CurrentCulture {
            get {
                return this.currentCulture;
            }
        }
        public static IUpdateServer GetUpdateServer() {
            IUpdateServer updateServer = AdminProxy.GetUpdateServer();
            return updateServer;
        }
        public static IUpdateServer GetUpdateServer(
          string serverName,
          bool useSecureConnection) {
            IUpdateServer updateServer = AdminProxy.GetUpdateServer(serverName, useSecureConnection);
            return updateServer;
        }
        public static IUpdateServer GetUpdateServer(
          string serverName,
          bool useSecureConnection,
          int portNumber) {
            IUpdateServer updateServer = AdminProxy.GetUpdateServer(serverName, useSecureConnection, portNumber);
            return updateServer;
        }
    }
}
