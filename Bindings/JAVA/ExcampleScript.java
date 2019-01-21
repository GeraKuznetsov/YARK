
import Client.Client;
import static Client.Client.*;

public class ExcampleScript {

    static int abortTime;
    static int state = 0;

    public static void main(String... args) {
        Client c = new Client();
        c.Connect("localhost", 9999);
        System.out.println("Waiting For Connection...");
        c.WaitForConnection();
        if (c.GetState() == TCP_FAILED) {
            System.out.println("Failed To Connect:");
            System.out.println(c.error);
        } else {
            System.out.println("Connected!");
            while (c.GetState() == TCP_CONNECTED) {
                if (c.Status.inFlight) {// if there is a vessel in flight
                    switch (state) {
                        case 0:
                            if (c.Vessel.MissionTime > 1 & c.Vessel.CurrentStage - c.Vessel.TotalStage != 0) {
                                System.out.println("Flight Started: " + c.Status.vesselName);
                                state = 1;
                            }
                            break;
                        case 1:
                            if (c.Vessel.Prograde.Pitch < 0.f && c.Vessel.Alt < 70000) {
                                c.Control.SetMainControl(MC_ABORT, true); // stage abort
                                abortTime = c.Vessel.MissionTime; // record abort time
                                c.SendControls(); // send control packet
                                c.Control.SetMainControl(MC_ABORT, false); // reset abort
                                System.out.println("ABORTING\n");
                                state = 2;
                            }
                            break;
                        case 2:
                            if ((c.Vessel.MissionTime - abortTime) > 2 & c.Vessel.Alt < 10000 & c.Vessel.Prograde.Pitch < 70) {
                                System.out.println("ABORTING SEQUENCE STEP 2\n");
                                c.Control.SetActionGroup(AG_1, true); //Activate parachutes on action group 1
                                c.SendControls(); // send control packet
                                state = 3;
                            }
                            break;
                    }
                }
            }
            c.Shutdown();
        }
    }
}
