﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apprenda.SaaSGrid.Addons;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Apprenda.Services.Logging;
using System.Net;

namespace Apprenda.Bluemix.AddOn
{
    public class BluemixAddon : AddonBase
    {
        string end_point;
        string bm_version;

        private static readonly ILogger log = LogManager.Instance().GetLogger(typeof(BluemixAddon));

        public override OperationResult Deprovision(AddonDeprovisionRequest _request)
        {
            var deprovisionResult = new OperationResult { EndUserMessage = string.Empty, IsSuccess = false };

            var manifest = _request.Manifest;
            var devParameters = _request.DeveloperParameters;
            var devOptions = BMDeveloperOptions.Parse(devParameters, manifest);

            end_point = devOptions.api_url;
            bm_version = devOptions.api_version;

            try
            {
                var token = authenticate(devOptions.bluemixuser, devOptions.bluemixpass);
                string name = devOptions.name;
                var serviceGUID = getServiceGUID(token, name);
                var status = deleteServiceInstance(token, name, serviceGUID);
                log.Info("BluemixAddon Deprovisioned Successfully");
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("BluemixAddon Deprovision Error: {0}\n{1}", ex, ex.StackTrace);
                log.Error(errorMessage);
                deprovisionResult.EndUserMessage = errorMessage;
                return deprovisionResult;
            }

            deprovisionResult.IsSuccess = true;
            return deprovisionResult;
        }

        public override ProvisionAddOnResult Provision(AddonProvisionRequest _request)
        {
            var provisionResult = new ProvisionAddOnResult(string.Empty) { IsSuccess = false };

            var manifest = _request.Manifest;
            var devParameters = _request.DeveloperParameters;
            var devOptions = BMDeveloperOptions.Parse(devParameters, manifest);

            end_point = devOptions.api_url;
            bm_version = devOptions.api_version;

            string instanceDetails = "";

            try
            {
                var token = authenticate(devOptions.bluemixuser, devOptions.bluemixpass);
                var servicePlansURL = getServicePlansURL(token, devOptions.servicename);
                var servicePlanGUID = getServicePlanGUID(token, servicePlansURL);
                string name = devOptions.name;
                var spaceGUID = getSpaceGuid(token, devOptions.space);
                var serviceInstanceGUID = createServiceInstance(token, name, servicePlanGUID, spaceGUID);
                instanceDetails = createInstanceDetails(token, name, serviceInstanceGUID);
                log.Info("BluemixAddon Provisioned Successfully");
            }
            catch (Exception ex) {
                var errorMessage = string.Format("BluemixAddon Provisioning Error: {0}\n{1}", ex, ex.StackTrace);
                log.Error(errorMessage);
                provisionResult.EndUserMessage = errorMessage;
                return provisionResult;
            }

            provisionResult.IsSuccess = true;
            provisionResult.ConnectionData = instanceDetails;
            return provisionResult;
        }

        public override OperationResult Test(AddonTestRequest _request)
        {
            var manifest = _request.Manifest;
            var devParameters = _request.DeveloperParameters;
            var devOptions = BMDeveloperOptions.Parse(devParameters, manifest);
            var testResult = new OperationResult { EndUserMessage = string.Empty, IsSuccess = false };

            end_point = devOptions.api_url;
            bm_version = devOptions.api_version;

            try
            {
                var token = authenticate(devOptions.bluemixuser, devOptions.bluemixpass);
                var servicePlansURL = getServicePlansURL(token, devOptions.servicename);
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("BluemixAddon Testing Error: {0}\n{1}", ex, ex.StackTrace);
                log.Error(errorMessage);
                testResult.EndUserMessage = errorMessage;
                return testResult;
            }

            testResult.EndUserMessage = "BluemixAddon Add-On was tested successfully";
            testResult.IsSuccess = true;
            return testResult;
        }

        public string authenticate(string user, string pass)
        {
            var username = Uri.EscapeDataString(user);
            var password = Uri.EscapeDataString(pass);
            var client = new RestClient("https://login.ng.bluemix.net/UAALoginServerWAR/oauth/token");
            var request = new RestRequest(Method.POST);
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/x-www-form-urlencoded");
            request.AddHeader("authorization", "Basic Y2Y6");
            var authString = string.Format("grant_type=password&username={0}&password={1}", username, password);
            log.Fatal("auth: " + authString);
            request.AddParameter("application/x-www-form-urlencoded", authString, ParameterType.RequestBody);
            try
            {
                IRestResponse response = client.Execute(request);
                var json_response = JsonConvert.DeserializeObject<dynamic>(response.Content);
                var token = json_response.access_token;
                return token;
            }catch(Exception ex)
            {
                log.Info("Error Authenticating with Bluemix: " + ex);
                throw;
            }
        }

        public string getServicePlansURL(string token, string name)
        {
            var url = string.Format("{0}/{1}/services?q=label:{2}", end_point, bm_version, name);
            var client = new RestClient(url);
            var request = new RestRequest(Method.GET);
            request.AddHeader("authorization", "bearer " + token);
            try
            {
                IRestResponse response = client.Execute(request);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response.Content);
                var responseArray = responseObject.resources;

                if (responseObject.total_results == 0)
                {
                    throw new Exception("Service not found");
                }

                var service_plans_url = responseArray[0].entity.service_plans_url;
                return service_plans_url;
            }catch(Exception ex)
            {
                log.Info("Error getting ServicePlanPlansURL: " + ex);
                throw;
            }
        }

        public string getServicePlanGUID(string token, string service_plans_url)
        {
            var url = string.Format("{0}{1}", end_point, service_plans_url);
            var client = new RestClient(url);
            var request = new RestRequest(Method.GET);
            request.AddHeader("authorization", "bearer " + token);
            try
            {
                IRestResponse response = client.Execute(request);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response.Content);
                return responseObject.resources[0].metadata.guid;
            }catch(Exception ex)
            {
                log.Info("Error getting ServicePlanGUID: " + ex);
                throw;
            }
        }

        public string getServiceGUID(string token, string name)
        {
            var url = string.Format("{0}/{1}/service_instances?q=name:{2}", end_point, bm_version, name);
            var client = new RestClient(url);
            var request = new RestRequest(Method.GET);
            request.AddHeader("authorization", "bearer " + token);
            try
            {
                IRestResponse response = client.Execute(request);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response.Content);
                return responseObject.resources[0].metadata.guid;
            }catch(Exception ex)
            {
                log.Info("Error getting ServiceGUID: " + ex);
                throw;
            }
        }

        public string createServiceInstance(string token, string name, string servicePlanGUID, string spaceGUID)
        {
            var url = string.Format("{0}/{1}/service_instances", end_point, bm_version);
            var client = new RestClient(url);
            var request = new RestRequest(Method.POST);
            request.AddHeader("authorization", "bearer " + token);

            dynamic body = new JObject();
            body.name = name;
            body.space_guid = spaceGUID;
            body.service_plan_guid = servicePlanGUID;
            request.AddParameter("application/json", body, ParameterType.RequestBody);

            try
            {
                IRestResponse response = client.Execute(request);
                log.Info("Service instance created successfully");
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response.Content);
                return responseObject.metadata.guid;
            }
            catch (Exception ex)
            {
                log.Info("Error creating service instance: " + ex);
                throw;
            }

        }

        public string getSpaceGuid(string token, string name)
        {
            var url = string.Format("{0}/{1}/spaces?q=name:{2}", end_point, bm_version, name);
            var client = new RestClient(url);
            var request = new RestRequest(Method.GET);
            request.AddHeader("authorization", "bearer " + token);
            try
            {
                IRestResponse response = client.Execute(request);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response.Content);
                var responseArray = responseObject.resources;
                return responseArray[responseArray.Count - 1].metadata.guid;
            }catch(Exception ex)
            {
                log.Info("Error getting SpaceGUID: " + ex);
                throw;
            }
        }

        public string createInstanceDetails(string token, string name, string serviceInstanceGUID)
        {
            var url = string.Format("{0}/{1}/service_keys", end_point, bm_version);
            var client = new RestClient(url);
            var request = new RestRequest(Method.POST);
            request.AddHeader("authorization", "bearer " + token);

            dynamic body = new JObject();
            body.name = name;
            body.service_instance_guid = serviceInstanceGUID;
            request.AddParameter("application/json", body, ParameterType.RequestBody);

            try
            {
                IRestResponse response = client.Execute(request);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(response.Content);
                JObject credentials = responseObject.entity.credentials;
                //change format to connection string
                return credentials.ToString(Formatting.None).Trim('{', '}').Replace(',', ';');
            } catch(Exception ex)
            {
                log.Info("Error requesting instance details: " + ex);
                throw;
            }
        }

        public string deleteServiceInstance(string token, string name, string serviceInstanceGUID)
        {
            var url = string.Format("{0}/{1}/service_instances/{2}?recursive=true&async=true", end_point, bm_version, serviceInstanceGUID);
            var client = new RestClient(url);
            var request = new RestRequest(Method.DELETE);
            request.AddHeader("authorization", "bearer " + token);

            try
            {
                IRestResponse response = client.Execute(request);
                log.Info("Service instance deleted successfully");
                return response.StatusCode.ToString();
            }
            catch (Exception ex)
            {
                log.Info("Error creating service instance: " + ex);
                throw;
            }

        }
    }
}
