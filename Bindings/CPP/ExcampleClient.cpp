/*
This is a simple script that checks for abort conditions and excecutes a very simple two step abort sequence
*/

#include <iostream>
#include <string>
#include "Client/Client.h"

int abortTime;
int step = 0;

void main() {
	Client* c = new Client();
	c->Connect("localhost", "9999");
	c->WaitForConnection();
	while (c->GetState() == TCP_CONNECTED) {
		if (c->Status.inFlight) {//if there is a vessal in flight
			switch (step) {
			case 0:
				if (c->Vessel.MissionTime > 0.5f & c->Vessel.CurrentStage != c->Vessel.TotalStage) { //if there was a staging, flight has started
					printf(c->Status.vesselName);
					printf(": Flight Started\n");
					step = 1;
				}
				break;
			case 1:
				if (c->Vessel.Prograde.Pitch < 0.f && c->Vessel.Alt < 70000) { //if our prograde vector's pitch is negative (aka we are falling),
																					   //and we are within the atmoshere, activate abort sequence
					c->Control.SetMainControl(MC_ABORT, true); //stage abort
					abortTime = c->Vessel.MissionTime; //record abort time
					c->SendControls(); //send control packet
					c->Control.SetMainControl(MC_ABORT, false); //reset abort
					printf("ABORTING\n");
					step = 2;
				}
				break;
			case 2:
				if ((c->Vessel.MissionTime - abortTime) > 1 & c->Vessel.Alt < 10000) { //after two seconds...
					printf("ABORTING SEQUENCE STEP 2\n");
					c->Control.SetActionGroup(AG_1, true);
					c->SendControls(); //send control packet
					step = 3;
				}
				break;
			}
		}
	}
	printf("Error: %s\n", c->error);
}